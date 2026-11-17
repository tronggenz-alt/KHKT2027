//
// BioWebViewPlugin.mm
// Native iOS WKWebView bridge for VERALab 3D Biology Lab
//

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <WebKit/WebKit.h>

#if defined(__cplusplus)
extern "C" {
#endif
    // Unity iOS runtime exports
    UIViewController* UnityGetGLViewController(void);
    void UnitySendMessage(const char* obj, const char* method, const char* msg);

    // Exported plugin C API
    void _OpenBioWebView(const char* urlOrRelativePath);
    void _CloseBioWebView(void);
#if defined(__cplusplus)
}
#endif

#pragma mark - Helper to locate root UIViewController

static UIViewController* BioGetRootViewController(void) {
    UIViewController* vc = nil;
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wundeclared-selector"
    if (&UnityGetGLViewController != NULL) {
        vc = UnityGetGLViewController();
    }
#pragma clang diagnostic pop
    if (vc != nil) {
        return vc;
    }

    if (@available(iOS 13.0, *)) {
        for (UIScene* scene in [UIApplication sharedApplication].connectedScenes) {
            if (scene.activationState == UISceneActivationStateForegroundActive &&
                [scene isKindOfClass:[UIWindowScene class]]) {
                UIWindowScene* windowScene = (UIWindowScene*)scene;
                for (UIWindow* w in windowScene.windows) {
                    if (w.isKeyWindow && w.rootViewController != nil) {
                        return w.rootViewController;
                    }
                }
            }
        }
    }

    #pragma clang diagnostic push
    #pragma clang diagnostic ignored "-Wdeprecated-declarations"
    UIWindow* keyWindow = [UIApplication sharedApplication].keyWindow;
    if (keyWindow && keyWindow.rootViewController != nil) {
        return keyWindow.rootViewController;
    }
    #pragma clang diagnostic pop

    return nil;
}

#pragma mark - BioWebViewManager Interface

@interface BioWebViewManager : NSObject <WKScriptMessageHandler, WKNavigationDelegate>
@property (nonatomic, strong) WKWebView* webView;
@property (nonatomic, strong) UIButton* closeButton;
@property (nonatomic, assign) BOOL isOpen;

+ (instancetype)sharedInstance;
- (void)openWithUrlOrPath:(const char*)urlOrRelativePath;
- (void)close;
@end

#pragma mark - BioWebViewManager Implementation

@implementation BioWebViewManager

+ (instancetype)sharedInstance {
    static BioWebViewManager* s_instance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        s_instance = [[BioWebViewManager alloc] init];
    });
    return s_instance;
}

- (void)openWithUrlOrPath:(const char*)urlOrRelativePath {
    if (urlOrRelativePath == NULL || strlen(urlOrRelativePath) == 0) {
        NSLog(@"[BioWebViewPlugin] Error: Empty URL or path passed to _OpenBioWebView");
        return;
    }

    NSString* pathString = [NSString stringWithUTF8String:urlOrRelativePath];
    if (!pathString || pathString.length == 0) {
        NSLog(@"[BioWebViewPlugin] Error: Failed to parse path string");
        return;
    }

    dispatch_async(dispatch_get_main_queue(), ^{
        if (self.isOpen) {
            [self closeInternalCleanup];
        }

        UIViewController* rootVC = BioGetRootViewController();
        if (!rootVC || !rootVC.view) {
            NSLog(@"[BioWebViewPlugin] Error: Root UIViewController view is not available");
            return;
        }

        UIView* containerView = rootVC.view;

        // 1. Configure WKWebViewConfiguration with WebGL and script handlers
        WKWebViewConfiguration* config = [[WKWebViewConfiguration alloc] init];

        WKPreferences* prefs = [[WKPreferences alloc] init];
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
        prefs.javaScriptEnabled = YES;
#pragma clang diagnostic pop
#if __IPHONE_OS_VERSION_MAX_ALLOWED >= 140000
        if (@available(iOS 14.0, *)) {
            WKWebpagePreferences* webpagePrefs = [[WKWebpagePreferences alloc] init];
            webpagePrefs.allowsContentJavaScript = YES;
            config.defaultWebpagePreferences = webpagePrefs;
        }
#endif

        // Allow file access from file URLs
        [prefs setValue:@YES forKey:@"allowFileAccessFromFileURLs"];
        [config setValue:@YES forKey:@"allowUniversalAccessFromFileURLs"];

        config.preferences = prefs;
        config.allowsInlineMediaPlayback = YES;
        config.mediaTypesRequiringUserActionForPlayback = WKAudiovisualMediaTypeNone;

        // Script Message Handler for 'unity'
        WKUserContentController* userContent = [[WKUserContentController alloc] init];
        [userContent addScriptMessageHandler:self name:@"unity"];
        config.userContentController = userContent;

        // 2. Instantiate full-screen WKWebView
        self.webView = [[WKWebView alloc] initWithFrame:containerView.bounds configuration:config];
        self.webView.navigationDelegate = self;
        self.webView.autoresizingMask = UIViewAutoresizingFlexibleWidth | UIViewAutoresizingFlexibleHeight;
        self.webView.backgroundColor = [UIColor colorWithRed:0.04f green:0.07f blue:0.12f alpha:1.0f]; // Deep dark background
        self.webView.opaque = YES;
        self.webView.scrollView.bounces = NO;
        if (@available(iOS 11.0, *)) {
            self.webView.scrollView.contentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentNever;
        }

        [containerView addSubview:self.webView];

        // 3. Floating native back/close button '← Về Lab' in top-left safe area as fail-safe
        self.closeButton = [UIButton buttonWithType:UIButtonTypeCustom];
        [self.closeButton setTitle:@"← Về Lab" forState:UIControlStateNormal];
        [self.closeButton setTitleColor:[UIColor colorWithRed:0.95f green:0.98f blue:1.0f alpha:1.0f] forState:UIControlStateNormal];
        [self.closeButton setTitleColor:[UIColor colorWithRed:0.55f green:0.75f blue:0.90f alpha:1.0f] forState:UIControlStateHighlighted];
        self.closeButton.titleLabel.font = [UIFont systemFontOfSize:14 weight:UIFontWeightSemibold];
        self.closeButton.backgroundColor = [UIColor colorWithRed:0.08f green:0.13f blue:0.20f alpha:0.90f];
        self.closeButton.layer.cornerRadius = 18.0f;
        self.closeButton.layer.borderWidth = 1.2f;
        self.closeButton.layer.borderColor = [UIColor colorWithRed:0.13f green:0.77f blue:0.93f alpha:0.80f].CGColor; // Cyan #22D3EE
        self.closeButton.layer.shadowColor = [UIColor blackColor].CGColor;
        self.closeButton.layer.shadowOffset = CGSizeMake(0, 2);
        self.closeButton.layer.shadowRadius = 4.0f;
        self.closeButton.layer.shadowOpacity = 0.5f;
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
        self.closeButton.contentEdgeInsets = UIEdgeInsetsMake(8, 16, 8, 16);
#pragma clang diagnostic pop
        self.closeButton.translatesAutoresizingMaskIntoConstraints = NO;

        [containerView addSubview:self.closeButton];

        if (@available(iOS 11.0, *)) {
            UILayoutGuide* safeGuide = containerView.safeAreaLayoutGuide;
            [NSLayoutConstraint activateConstraints:@[
                [self.closeButton.topAnchor constraintEqualToAnchor:safeGuide.topAnchor constant:12.0f],
                [self.closeButton.leadingAnchor constraintEqualToAnchor:safeGuide.leadingAnchor constant:16.0f],
                [self.closeButton.heightAnchor constraintGreaterThanOrEqualToConstant:36.0f]
            ]];
        } else {
            [NSLayoutConstraint activateConstraints:@[
                [self.closeButton.topAnchor constraintEqualToAnchor:containerView.topAnchor constant:28.0f],
                [self.closeButton.leadingAnchor constraintEqualToAnchor:containerView.leadingAnchor constant:16.0f],
                [self.closeButton.heightAnchor constraintGreaterThanOrEqualToConstant:36.0f]
            ]];
        }

        [self.closeButton addTarget:self action:@selector(onCloseButtonTapped:) forControlEvents:UIControlEventTouchUpInside];
        [containerView bringSubviewToFront:self.closeButton];

        // 4. Load URL / File URL
        if ([pathString hasPrefix:@"http://"] || [pathString hasPrefix:@"https://"]) {
            NSURL* url = [NSURL URLWithString:pathString];
            [self.webView loadRequest:[NSURLRequest requestWithURL:url]];
        } else {
            NSURL* fileURL = nil;
            if ([pathString hasPrefix:@"file://"]) {
                fileURL = [NSURL URLWithString:pathString];
            } else {
                fileURL = [NSURL fileURLWithPath:pathString];
            }

            // Grant read access to the parent/grandparent directory for relative vendor & 3d assets
            NSURL* baseDir = [fileURL URLByDeletingLastPathComponent];
            NSURL* parentDir = [baseDir URLByDeletingLastPathComponent];
            NSURL* readAccessDir = (parentDir && parentDir.path.length > 0) ? parentDir : baseDir;

            if (@available(iOS 9.0, *)) {
                [self.webView loadFileURL:fileURL allowingReadAccessToURL:readAccessDir];
            } else {
                [self.webView loadRequest:[NSURLRequest requestWithURL:fileURL]];
            }
        }

        self.isOpen = YES;
        NSLog(@"[BioWebViewPlugin] BioWebView successfully opened: %@", pathString);
    });
}

- (void)onCloseButtonTapped:(UIButton*)sender {
    NSLog(@"[BioWebViewPlugin] Close button tapped");
    [self close];
}

- (void)closeInternalCleanup {
    if (self.closeButton) {
        [self.closeButton removeFromSuperview];
        self.closeButton = nil;
    }

    if (self.webView) {
        [self.webView.configuration.userContentController removeScriptMessageHandlerForName:@"unity"];
        self.webView.navigationDelegate = nil;
        [self.webView stopLoading];
        [self.webView removeFromSuperview];
        self.webView = nil;
    }

    self.isOpen = NO;
}

- (void)close {
    if (!self.isOpen) {
        return;
    }

    dispatch_async(dispatch_get_main_queue(), ^{
        [self closeInternalCleanup];

        UIViewController* rootVC = BioGetRootViewController();
        if (rootVC && [rootVC.view canBecomeFirstResponder]) {
            [rootVC.view becomeFirstResponder];
        }

        NSLog(@"[BioWebViewPlugin] BioWebView closed, notifying Unity (BioWebViewBridge.OnWebViewClosed)");
        UnitySendMessage("BioWebViewBridge", "OnWebViewClosed", "");
    });
}

#pragma mark - WKScriptMessageHandler

- (void)userContentController:(WKUserContentController *)userContentController didReceiveScriptMessage:(WKScriptMessage *)message {
    if ([message.name isEqualToString:@"unity"]) {
        id body = message.body;
        NSLog(@"[BioWebViewPlugin] Received message from JS 'unity': %@", body);

        if ([body isKindOfClass:[NSString class]]) {
            NSString* str = (NSString*)body;
            if ([str isEqualToString:@"close"]) {
                [self close];
                return;
            }
        } else if ([body isKindOfClass:[NSDictionary class]]) {
            NSDictionary* dict = (NSDictionary*)body;
            if ([@"close" isEqualToString:dict[@"action"]] ||
                [@"close" isEqualToString:dict[@"type"]] ||
                [@"close" isEqualToString:dict[@"event"]]) {
                [self close];
                return;
            }
        }

        // Fallback check on string representation
        if ([[body description] isEqualToString:@"close"]) {
            [self close];
        }
    }
}

#pragma mark - WKNavigationDelegate

- (void)webView:(WKWebView *)webView decidePolicyForNavigationAction:(WKNavigationAction *)navigationAction decisionHandler:(void (^)(WKNavigationActionPolicy))decisionHandler {
    NSURL* requestUrl = navigationAction.request.URL;
    NSString* scheme = requestUrl.scheme.lowercaseString;
    NSString* abs = requestUrl.absoluteString.lowercaseString;

    // Intercept veralab://close navigation
    if ([scheme isEqualToString:@"veralab"] || [abs hasPrefix:@"veralab://close"]) {
        NSLog(@"[BioWebViewPlugin] Intercepted navigation: %@ -> Closing BioWebView", requestUrl);
        decisionHandler(WKNavigationActionPolicyCancel);
        [self close];
        return;
    }

    decisionHandler(WKNavigationActionPolicyAllow);
}

- (void)webView:(WKWebView *)webView didFailNavigation:(WKNavigation *)navigation withError:(NSError *)error {
    if (error.code != NSURLErrorCancelled) {
        NSLog(@"[BioWebViewPlugin] Navigation error: %@", error.localizedDescription);
    }
}

- (void)webView:(WKWebView *)webView didFailProvisionalNavigation:(WKNavigation *)navigation withError:(NSError *)error {
    if (error.code != NSURLErrorCancelled) {
        NSLog(@"[BioWebViewPlugin] Provisional navigation error: %@", error.localizedDescription);
    }
}

@end

#pragma mark - C Interface Implementations

void _OpenBioWebView(const char* urlOrRelativePath) {
    [[BioWebViewManager sharedInstance] openWithUrlOrPath:urlOrRelativePath];
}

void _CloseBioWebView(void) {
    [[BioWebViewManager sharedInstance] close];
}
