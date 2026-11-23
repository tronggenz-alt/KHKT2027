// BioWebViewMac.mm — WKWebView cho bản Mac của VERA Lab (giống Plugins/iOS/BioWebViewPlugin.mm).
// Phủ trang biology-lab-3d.html lên cửa sổ Unity, có nút "← Về Lab"; đóng khi bấm nút,
// khi trang gửi webkit.messageHandlers.unity.postMessage('close') hoặc điều hướng veralab://close.
// C# hỏi _BioMac_ConsumeClosed() mỗi khung hình (bản Mac không có UnitySendMessage).
// Dựng lại: bash build.sh  → ../BioWebViewMac.bundle
#import <Cocoa/Cocoa.h>
#import <WebKit/WebKit.h>

@interface BioMacWeb : NSObject <WKScriptMessageHandler, WKNavigationDelegate, WKUIDelegate>
@property (nonatomic, strong) NSView* host;
@property (nonatomic, strong) WKWebView* web;
@property (nonatomic, strong) NSButton* backButton;
@property (nonatomic, weak) NSResponder* prevResponder;
@property (nonatomic, assign) BOOL open;
@property (nonatomic, assign) BOOL closedFlag;
@end

@implementation BioMacWeb
+ (instancetype)shared { static BioMacWeb* s; static dispatch_once_t t; dispatch_once(&t, ^{ s = [BioMacWeb new]; }); return s; }

- (BOOL)openPath:(NSString*)path {
    if (self.open) [self teardown];
    NSWindow* w = [NSApp keyWindow] ?: [NSApp mainWindow];
    if (!w) for (NSWindow* x in [NSApp windows]) if (x.isVisible) { w = x; break; }
    if (!w || !w.contentView) return NO;
    NSView* cv = w.contentView;

    WKWebViewConfiguration* cfg = [WKWebViewConfiguration new];
    @try { [cfg.preferences setValue:@YES forKey:@"allowFileAccessFromFileURLs"]; } @catch (NSException* e) {}
    @try { [cfg setValue:@YES forKey:@"allowUniversalAccessFromFileURLs"]; } @catch (NSException* e) {}
    [cfg.userContentController addScriptMessageHandler:self name:@"unity"];

    NSView* host = [[NSView alloc] initWithFrame:cv.bounds];
    host.autoresizingMask = NSViewWidthSizable | NSViewHeightSizable;
    host.wantsLayer = YES;
    host.layer.backgroundColor = [NSColor colorWithRed:0.04 green:0.07 blue:0.12 alpha:1].CGColor;

    WKWebView* web = [[WKWebView alloc] initWithFrame:host.bounds configuration:cfg];
    web.autoresizingMask = NSViewWidthSizable | NSViewHeightSizable;
    web.navigationDelegate = self;
    web.UIDelegate = self;
    [host addSubview:web];

    NSButton* b = [NSButton buttonWithTitle:@"← Về Lab" target:self action:@selector(onBack:)];
    b.bordered = NO;
    b.wantsLayer = YES;
    b.layer.backgroundColor = [NSColor colorWithRed:0.08 green:0.13 blue:0.20 alpha:0.92].CGColor;
    b.layer.cornerRadius = 18;
    b.layer.borderWidth = 1.2;
    b.layer.borderColor = [NSColor colorWithRed:0.13 green:0.77 blue:0.93 alpha:0.8].CGColor;
    b.attributedTitle = [[NSAttributedString alloc] initWithString:@"← Về Lab" attributes:@{
        NSForegroundColorAttributeName: [NSColor whiteColor],
        NSFontAttributeName: [NSFont systemFontOfSize:14 weight:NSFontWeightSemibold] }];
    b.frame = NSMakeRect(16, 16, 112, 36);                 // góc dưới trái: không che thanh tiêu đề của trang
    b.autoresizingMask = NSViewMaxYMargin | NSViewMaxXMargin;
    b.hidden = YES;                                        // chỉ hiện khi trang không có nút "Về phòng Lab 3D" riêng
    [host addSubview:b];
    self.backButton = b;

    [cv addSubview:host positioned:NSWindowAbove relativeTo:nil];
    self.host = host; self.web = web;
    self.prevResponder = w.firstResponder;

    if ([path hasPrefix:@"http://"] || [path hasPrefix:@"https://"]) {
        [web loadRequest:[NSURLRequest requestWithURL:[NSURL URLWithString:path]]];
    } else {
        NSURL* url = [path hasPrefix:@"file://"] ? [NSURL URLWithString:path] : [NSURL fileURLWithPath:path];
        [web loadFileURL:url allowingReadAccessToURL:[url URLByDeletingLastPathComponent]];
    }
    [w makeFirstResponder:web];
    self.open = YES; self.closedFlag = NO;
    return YES;
}

- (void)teardown {
    if (self.web) {
        [self.web.configuration.userContentController removeScriptMessageHandlerForName:@"unity"];
        self.web.navigationDelegate = nil;
        self.web.UIDelegate = nil;
        [self.web stopLoading];
    }
    NSWindow* w = self.host.window;
    [self.host removeFromSuperview];
    self.host = nil; self.web = nil; self.backButton = nil;
    if (w) [w makeFirstResponder:self.prevResponder ?: w.contentView];
    self.open = NO;
}

- (void)closeAndNotify { if (!self.open) return; [self teardown]; self.closedFlag = YES; }
- (void)onBack:(id)s { [self closeAndNotify]; }

- (void)userContentController:(WKUserContentController*)u didReceiveScriptMessage:(WKScriptMessage*)m {
    id body = m.body;
    BOOL close = [[body description] isEqualToString:@"close"];
    if ([body isKindOfClass:[NSDictionary class]]) {
        NSDictionary* d = body;
        close = close || [@"close" isEqual:d[@"action"]] || [@"close" isEqual:d[@"type"]] || [@"close" isEqual:d[@"event"]];
    }
    if (close) dispatch_async(dispatch_get_main_queue(), ^{ [self closeAndNotify]; });
}

- (void)webView:(WKWebView*)v decidePolicyForNavigationAction:(WKNavigationAction*)a decisionHandler:(void (^)(WKNavigationActionPolicy))h {
    NSURL* u = a.request.URL;
    if ([u.scheme.lowercaseString isEqualToString:@"veralab"]) {
        h(WKNavigationActionPolicyCancel);
        dispatch_async(dispatch_get_main_queue(), ^{ [self closeAndNotify]; });
        return;
    }
    h(WKNavigationActionPolicyAllow);
}

- (void)webView:(WKWebView*)v didFinishNavigation:(WKNavigation*)n {
    [v evaluateJavaScript:@"!!document.getElementById('nut-ve-lab')" completionHandler:^(id r, NSError* e) {
        self.backButton.hidden = (e == nil && [r respondsToSelector:@selector(boolValue)] && [r boolValue]);
    }];
}
- (void)webView:(WKWebView*)v didFailNavigation:(WKNavigation*)n withError:(NSError*)e { self.backButton.hidden = NO; }
- (void)webView:(WKWebView*)v didFailProvisionalNavigation:(WKNavigation*)n withError:(NSError*)e { self.backButton.hidden = NO; }
- (void)webViewWebContentProcessDidTerminate:(WKWebView*)v { self.backButton.hidden = NO; }

// target=_blank / window.open: liên kết web mở bằng trình duyệt mặc định
- (WKWebView*)webView:(WKWebView*)v createWebViewWithConfiguration:(WKWebViewConfiguration*)c forNavigationAction:(WKNavigationAction*)a windowFeatures:(WKWindowFeatures*)f {
    NSURL* u = a.request.URL;
    if ([u.scheme isEqualToString:@"http"] || [u.scheme isEqualToString:@"https"]) [[NSWorkspace sharedWorkspace] openURL:u];
    return nil;
}

// <input type=file>, alert/confirm của trang
- (void)webView:(WKWebView*)v runOpenPanelWithParameters:(WKOpenPanelParameters*)p initiatedByFrame:(WKFrameInfo*)fr completionHandler:(void (^)(NSArray<NSURL*>*))done {
    NSOpenPanel* op = [NSOpenPanel openPanel];
    op.allowsMultipleSelection = p.allowsMultipleSelection;
    [op beginSheetModalForWindow:v.window completionHandler:^(NSModalResponse r) { done(r == NSModalResponseOK ? op.URLs : nil); }];
}
- (void)webView:(WKWebView*)v runJavaScriptAlertPanelWithMessage:(NSString*)msg initiatedByFrame:(WKFrameInfo*)fr completionHandler:(void (^)(void))done {
    NSAlert* al = [NSAlert new]; al.messageText = msg; [al runModal]; done();
}
- (void)webView:(WKWebView*)v runJavaScriptConfirmPanelWithMessage:(NSString*)msg initiatedByFrame:(WKFrameInfo*)fr completionHandler:(void (^)(BOOL))done {
    NSAlert* al = [NSAlert new]; al.messageText = msg; [al addButtonWithTitle:@"OK"]; [al addButtonWithTitle:@"Huỷ"];
    done([al runModal] == NSAlertFirstButtonReturn);
}
@end

static void BioRunMain(dispatch_block_t b) { if ([NSThread isMainThread]) b(); else dispatch_sync(dispatch_get_main_queue(), b); }

extern "C" {
__attribute__((visibility("default"))) int _BioMac_Open(const char* path) {
    if (!path || !*path) return 0;
    NSString* p = [NSString stringWithUTF8String:path];
    __block BOOL ok = NO;
    BioRunMain(^{ ok = [[BioMacWeb shared] openPath:p]; });
    return ok ? 1 : 0;
}
__attribute__((visibility("default"))) void _BioMac_Close(void) { BioRunMain(^{ [[BioMacWeb shared] closeAndNotify]; }); }
__attribute__((visibility("default"))) int _BioMac_IsOpen(void) { return [BioMacWeb shared].open ? 1 : 0; }
__attribute__((visibility("default"))) int _BioMac_ConsumeClosed(void) {
    BioMacWeb* s = [BioMacWeb shared];
    if (!s.closedFlag) return 0;
    s.closedFlag = NO;
    return 1;
}
}
