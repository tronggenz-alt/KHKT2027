using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Khkt.Network
{
    /// <summary>
    /// Thân người 3D của một người cùng phòng (nhân vật Humanoid, ví dụ tải từ Mixamo).
    ///  • Chuyển động đứng chờ / đi bộ / chạy lấy từ file (quay từ người thật), trộn mượt theo tốc độ di chuyển của đầu;
    ///    xoay tại chỗ thì bước chân nhẹ, rẽ thì nghiêng người vào trong, đi thì hơi đổ người về trước.
    ///  • Chân đặt trên sàn ngay dưới đầu; người quay theo hướng đi, đứng yên thì quay theo hướng nhìn.
    ///  • Đầu quay theo hướng nhìn; tay phải với tới bàn tay ảo của người đó (IK hai xương), cổ tay xoay theo
    ///    bàn tay ảo (thấy rõ lúc nghiêng cốc rót), ngón tay nắm lại khi đang cầm đồ.
    /// Người chơi không thấy thân mình: góc nhìn của mình vẫn là hai tay của ArmRig.
    ///
    /// File đặt trong Assets/Lab/NguoiChoi/Resources/NguoiChoi/: NhanVat.fbx (nhân vật, With Skin),
    /// DungYen.fbx (đứng chờ), DiBo.fbx (đi bộ, In Place), Chay.fbx (chạy, In Place, không bắt buộc).
    /// Thiếu nhân vật hoặc thiếu cả chuyển động đứng chờ thì <see cref="Create"/> trả null và LabPresence dùng hình khối.
    /// </summary>
    public class RemoteAvatar : MonoBehaviour
    {
        public const string ModelPath = "NguoiChoi/NhanVat", IdlePath = "NguoiChoi/DungYen", WalkPath = "NguoiChoi/DiBo", RunPath = "NguoiChoi/Chay";
        /// <summary>Áo blouse trắng do NguoiChoiImporter tạo từ ảnh áo khoác của nhân vật (không có thì giữ áo gốc).</summary>
        public const string BlousePath = "NguoiChoi/AoBlouse";

        static readonly string[] OuterwearNames = { "suit", "jacket", "coat", "blazer", "blouse", "aokhoac" };

        /// <summary>Lưới áo khoác ngoài của nhân vật (theo tên lưới trong file, ví dụ "Ch23_Suit"): mặc áo blouse trắng.</summary>
        public static bool IsOuterwear(string meshName)
        {
            string n = meshName.ToLowerInvariant();
            foreach (var k in OuterwearNames) if (n.Contains(k)) return true;
            return false;
        }
        /// <summary>Tầm mắt khi đứng, bằng DesktopViewer.standingEyeHeight: nhân vật được thu phóng cho mắt ở đúng độ cao này.</summary>
        public const float EyeHeight = 1.6f;

        static bool warned;
        static readonly Dictionary<Material, Material> urpCopies = new Dictionary<Material, Material>();

        Animator animator;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable walkPlayable, runPlayable;
        bool hasWalk, hasRun;

        Renderer[] renderers;
        Transform head, neck, spine, upperArm, lowerArm, handBone, hips, footL, footR;
        Vector3 headFwdLocal, neckFwdLocal;
        float armReach, eyeAboveHead, ankleHeight;

        // bàn tay phải: trục ngón tay, mu bàn tay, trục gập ngón (trong hệ tọa độ của xương bàn tay, đo ở tư thế gốc)
        Vector3 handFingerLocal, handBackLocal, curlAxisLocal;
        Transform[][] fingers;   // ngón cái, trỏ, giữa, áp út, út; mỗi ngón 3 đốt (có thể thiếu)

        Vector3 lastHead;
        bool placed, turning;
        Vector3 velocity;
        float bodyYaw, prevBodyYaw, floorY, walkWeight, runBlend, reachWeight, grip, lean, forwardLean;

        Vector3 pendingLook, pendingHand;
        Quaternion pendingHandRot = Quaternion.identity;
        bool pendingHolding, hasPending;

        /// <summary>Tốc độ đi ngang hiện tại (m/s), cho bài kiểm thử.</summary>
        public float Speed => new Vector3(velocity.x, 0, velocity.z).magnitude;
        /// <summary>Trọng số chuyển động di chuyển đang trộn (0 = đứng chờ, 1 = đi / chạy).</summary>
        public float WalkWeight => walkWeight;
        /// <summary>Phần chạy trong chuyển động di chuyển (0 = đi bộ, 1 = chạy).</summary>
        public float RunBlend => runBlend;
        /// <summary>Trọng số tay phải đang với tới mục tiêu.</summary>
        public float ReachWeight => reachWeight;
        /// <summary>Mức nắm ngón tay phải (0 = theo chuyển động, 1 = nắm vật).</summary>
        public float Grip => grip;
        /// <summary>Độ nghiêng người khi rẽ (độ, dương = nghiêng trái).</summary>
        public float Lean => lean;
        public bool HasWalk => hasWalk;
        public bool HasRun => hasRun;
        public Transform HeadBone => head;
        /// <summary>Hướng mặt hiện tại của nhân vật (sau khi quay đầu).</summary>
        public Vector3 HeadForward => head != null ? head.rotation * headFwdLocal : transform.forward;
        public Transform RightHandBone => handBone;
        /// <summary>Hướng các ngón tay phải và mu bàn tay phải hiện tại (thế giới).</summary>
        public Vector3 HandFingerDir => handBone != null ? handBone.rotation * handFingerLocal : transform.right;
        public Vector3 HandBackDir => handBone != null ? handBone.rotation * handBackLocal : transform.up;
        /// <summary>Điểm trên đỉnh đầu nhân vật: bảng tên treo trên đó.</summary>
        public Vector3 HeadTop => head != null ? head.position + Vector3.up * (eyeAboveHead + 0.12f) : transform.position + Vector3.up * (EyeHeight + 0.15f);

        /// <summary>Dựng thân người từ file trong Resources; không có file hợp lệ thì trả null.</summary>
        public static RemoteAvatar Create(Transform parent, string name)
        {
            var prefab = Resources.Load<GameObject>(ModelPath);
            if (prefab == null) return null;
            var idle = FirstClip(IdlePath) ?? FirstClip(ModelPath);
            if (idle == null)
            {
                Warn("có NhanVat nhưng chưa có chuyển động đứng chờ (DungYen)");
                return null;
            }
            var go = Instantiate(prefab, parent, false);
            go.name = name;
            var anim = go.GetComponent<Animator>();
            if (anim == null || anim.avatar == null || !anim.avatar.isHuman)
            {
                Warn("NhanVat chưa nhập dạng Humanoid");
                Destroy(go);
                return null;
            }
            var a = go.AddComponent<RemoteAvatar>();
            a.Init(anim, idle, FirstClip(WalkPath), FirstClip(RunPath));
            return a;
        }

        static void Warn(string why)
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning("[NguoiChoi] Dùng hình khối thay thân người 3D: " + why);
        }

        static AnimationClip FirstClip(string path)
        {
            foreach (var c in Resources.LoadAll<AnimationClip>(path))
                if (c != null && !c.name.StartsWith("__preview__")) return c;
            return null;
        }

        void Init(Animator anim, AnimationClip idle, AnimationClip walk, AnimationClip run)
        {
            animator = anim;
            anim.runtimeAnimatorController = null;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            Transform B(HumanBodyBones b) => anim.GetBoneTransform(b);
            head = B(HumanBodyBones.Head);
            neck = B(HumanBodyBones.Neck);
            spine = B(HumanBodyBones.Spine);
            upperArm = B(HumanBodyBones.RightUpperArm);
            lowerArm = B(HumanBodyBones.RightLowerArm);
            handBone = B(HumanBodyBones.RightHand);
            hips = B(HumanBodyBones.Hips);
            footL = B(HumanBodyBones.LeftFoot);
            footR = B(HumanBodyBones.RightFoot);
            fingers = new[]
            {
                new[] { B(HumanBodyBones.RightThumbProximal), B(HumanBodyBones.RightThumbIntermediate), B(HumanBodyBones.RightThumbDistal) },
                new[] { B(HumanBodyBones.RightIndexProximal), B(HumanBodyBones.RightIndexIntermediate), B(HumanBodyBones.RightIndexDistal) },
                new[] { B(HumanBodyBones.RightMiddleProximal), B(HumanBodyBones.RightMiddleIntermediate), B(HumanBodyBones.RightMiddleDistal) },
                new[] { B(HumanBodyBones.RightRingProximal), B(HumanBodyBones.RightRingIntermediate), B(HumanBodyBones.RightRingDistal) },
                new[] { B(HumanBodyBones.RightLittleProximal), B(HumanBodyBones.RightLittleIntermediate), B(HumanBodyBones.RightLittleDistal) },
            };

            // Tư thế gốc trong file (T-pose) quay mặt theo +z của gốc: ghi lại trục "phía trước" của đầu, cổ
            var root = transform;
            // cổ chân cao hơn đế giày bao nhiêu (đứng thẳng trong tư thế gốc)
            ankleHeight = footL != null && footR != null ? Mathf.Min(footL.position.y, footR.position.y) - root.position.y : 0.08f;
            if (ankleHeight <= 0f || ankleHeight > 0.25f) ankleHeight = 0.08f;
            if (head != null) headFwdLocal = Quaternion.Inverse(head.rotation) * root.forward;
            if (neck != null) neckFwdLocal = Quaternion.Inverse(neck.rotation) * root.forward;
            if (upperArm != null && lowerArm != null && handBone != null)
            {
                armReach = (lowerArm.position - upperArm.position).magnitude + (handBone.position - lowerArm.position).magnitude;
                MeasureHand(root);
            }

            // Thu phóng cho mắt ở tầm EyeHeight. Mắt cao hơn khớp đầu khoảng 5% chiều cao người.
            float headY = head != null ? head.position.y - root.position.y : 0f;
            var eye = B(HumanBodyBones.LeftEye);
            float eyeY = eye != null ? eye.position.y - root.position.y : headY * 1.06f;
            if (eyeY > 0.05f)
            {
                float s = EyeHeight / eyeY;
                root.localScale *= s;
                armReach *= s;
                ankleHeight *= s;
                eyeAboveHead = (eyeY - headY) * s;
            }

            renderers = GetComponentsInChildren<Renderer>(true);
            var blouse = Resources.Load<Material>(BlousePath);
            foreach (var r in renderers)
            {
                if (blouse != null && IsOuterwear(r.name))
                {
                    var ms = r.sharedMaterials;
                    for (int i = 0; i < ms.Length; i++) ms[i] = blouse;
                    r.sharedMaterials = ms;
                }
                r.shadowCastingMode = Application.isMobilePlatform ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                r.sharedMaterials = FixMaterials(r.sharedMaterials);
                if (IsHairCards(r.name)) r.sharedMaterials = CutoutMaterials(r.sharedMaterials);
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }
            // tia chọn vật và người đi xuyên qua thân người khác, giống hình khối cũ
            foreach (var c in GetComponentsInChildren<Collider>(true)) Destroy(c);

            graph = PlayableGraph.Create("Người cùng phòng: " + name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, 3);
            // Playable mặc định bật Foot IK: clip thiếu đường cong đích bàn chân sẽ kéo chân lên sát hông.
            // Tắt đi, chân chạm sàn đã có phần bám sàn trong LateUpdate.
            var idlePlayable = AnimationClipPlayable.Create(graph, idle);
            idlePlayable.SetApplyFootIK(false);
            graph.Connect(idlePlayable, 0, mixer, 0);
            hasWalk = walk != null;
            if (hasWalk)
            {
                walkPlayable = AnimationClipPlayable.Create(graph, walk);
                walkPlayable.SetApplyFootIK(false);
                graph.Connect(walkPlayable, 0, mixer, 1);
            }
            hasRun = hasWalk && run != null;
            if (hasRun)
            {
                runPlayable = AnimationClipPlayable.Create(graph, run);
                runPlayable.SetApplyFootIK(false);
                graph.Connect(runPlayable, 0, mixer, 2);
            }
            mixer.SetInputWeight(0, 1f);
            var output = AnimationPlayableOutput.Create(graph, "Thân", anim);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        /// <summary>
        /// Đo trục bàn tay phải ở tư thế gốc: ngón tay chỉ từ cổ tay tới gốc ngón giữa (thiếu ngón thì theo cẳng tay),
        /// phía ngón cái từ ngón út sang ngón trỏ, mu bàn tay vuông góc hai trục đó.
        /// </summary>
        void MeasureHand(Transform root)
        {
            var mid = fingers[2][0];
            Vector3 fingerW = mid != null ? mid.position - handBone.position : handBone.position - lowerArm.position;
            fingerW = fingerW.sqrMagnitude > 1e-8f ? fingerW.normalized : root.right;
            Vector3 sideW = fingers[1][0] != null && fingers[4][0] != null ? fingers[1][0].position - fingers[4][0].position : root.forward;
            sideW -= fingerW * Vector3.Dot(sideW, fingerW);
            if (sideW.sqrMagnitude < 1e-8f) sideW = Vector3.Cross(Vector3.up, fingerW);
            sideW.Normalize();
            Vector3 backW = Vector3.Cross(sideW, fingerW).normalized;   // tay phải, lòng bàn tay úp: mu hướng lên
            var inv = Quaternion.Inverse(handBone.rotation);
            handFingerLocal = inv * fingerW;
            handBackLocal = inv * backW;
            curlAxisLocal = inv * Vector3.Cross(fingerW, -backW);       // gập ngón về phía lòng bàn tay
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        /// <summary>
        /// Gọi mỗi khung hình với vị trí đã làm mượt. headPos: mắt (camera) của người đó; look: hướng nhìn;
        /// handPos, handRot: bàn tay ảo (+z hướng ngón tay, +y mu bàn tay); holding: họ đang cầm vật.
        /// </summary>
        public void Drive(Vector3 headPos, Quaternion look, Vector3 handPos, Quaternion handRot, bool holding)
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 lookFwd = look * Vector3.forward;
            float lookYaw = Mathf.Atan2(lookFwd.x, lookFwd.z) * Mathf.Rad2Deg;

            // dịch chuyển trạm (nhảy xa) thì đặt lại, không tính thành vận tốc
            if (!placed || (headPos - lastHead).sqrMagnitude > 1.5f * 1.5f)
            {
                placed = true;
                velocity = Vector3.zero;
                walkWeight = 0f;
                runBlend = 0f;
                lean = forwardLean = 0f;
                turning = false;
                bodyYaw = prevBodyYaw = lookYaw;
                floorY = FindFloor(headPos);
            }
            else
            {
                Vector3 v = (headPos - lastHead) / dt;
                v.y = 0;
                velocity = Vector3.Lerp(velocity, v, 1f - Mathf.Exp(-8f * dt));
                floorY = Mathf.Lerp(floorY, FindFloor(headPos), 1f - Mathf.Exp(-10f * dt));
            }
            lastHead = headPos;

            // Hướng thân: đang đi thì theo hướng đi (lùi thì quay mặt ngược hướng đi và phát ngược),
            // đứng yên thì chỉ xoay khi đầu đã quay lệch quá xa
            float speed = Speed;
            float walkDir = 1f;
            float targetYaw = bodyYaw;
            if (speed > 0.25f)
            {
                float moveYaw = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
                if (Mathf.Abs(Mathf.DeltaAngle(lookYaw, moveYaw)) <= 110f) targetYaw = moveYaw;
                else { targetYaw = moveYaw + 180f; walkDir = -1f; }
                turning = false;
            }
            else
            {
                // đầu quay lệch quá 50° thì xoay người cho tới khi gần thẳng hướng nhìn
                float off = Mathf.Abs(Mathf.DeltaAngle(bodyYaw, lookYaw));
                if (off > 50f) turning = true;
                else if (off < 8f) turning = false;
                if (turning) targetYaw = lookYaw;
            }
            bodyYaw = Mathf.LerpAngle(bodyYaw, targetYaw, 1f - Mathf.Exp(-6f * dt));
            float yawRate = Mathf.DeltaAngle(prevBodyYaw, bodyYaw) / dt;   // độ/giây, dương = rẽ phải
            prevBodyYaw = bodyYaw;

            // gốc nhân vật dưới mắt, lùi lại một chút (mắt nằm trước cổ)
            var rot = Quaternion.Euler(0, bodyYaw, 0);
            Vector3 flat = new Vector3(lookFwd.x, 0, lookFwd.z);
            flat = flat.sqrMagnitude > 1e-4f ? flat.normalized : rot * Vector3.forward;
            Vector3 rootPos = headPos - flat * 0.08f;
            rootPos.y = floorY;
            transform.SetPositionAndRotation(rootPos, rot);

            // trộn đứng chờ / đi / chạy; xoay tại chỗ thì bước nhẹ cho chân không trượt trên sàn
            float move = hasWalk ? Mathf.Clamp01((speed - 0.15f) / 0.6f) : 0f;
            float shuffle = hasWalk && speed < 0.25f ? Mathf.Clamp01((Mathf.Abs(yawRate) - 30f) / 150f) * 0.45f : 0f;
            walkWeight = Mathf.Lerp(walkWeight, Mathf.Max(move, shuffle), 1f - Mathf.Exp(-8f * dt));
            runBlend = Mathf.Lerp(runBlend, hasRun ? Mathf.Clamp01((speed - 1.8f) / 0.8f) : 0f, 1f - Mathf.Exp(-6f * dt));
            if (mixer.IsValid())
            {
                mixer.SetInputWeight(0, 1f - walkWeight);
                mixer.SetInputWeight(1, walkWeight * (1f - runBlend));
                mixer.SetInputWeight(2, walkWeight * runBlend);
                if (hasWalk)
                {
                    // tốc độ phát khớp bước chân với tốc độ thật; chưa có clip chạy thì đi nhanh hơn
                    float walkRate = speed > 0.25f ? Mathf.Clamp(speed / 1.3f, 0.6f, hasRun ? 1.5f : 2.0f) : 0.8f;
                    walkPlayable.SetSpeed(walkDir * walkRate);
                }
                if (hasRun) runPlayable.SetSpeed(walkDir * Mathf.Clamp(speed / 3.0f, 0.7f, 1.4f));
            }

            // nghiêng người vào trong khi rẽ (như đi xe đạp), hơi đổ về trước khi đi nhanh
            float leanTarget = Mathf.Clamp(-yawRate * speed * 0.03f, -10f, 10f);
            lean = Mathf.Lerp(lean, leanTarget, 1f - Mathf.Exp(-5f * dt));
            forwardLean = Mathf.Lerp(forwardLean, walkDir * Mathf.Clamp(speed * 2.2f, 0f, 7f), 1f - Mathf.Exp(-4f * dt));  // đi lùi: hơi ngả ra sau

            pendingLook = lookFwd;
            pendingHand = handPos;
            pendingHandRot = handRot;
            pendingHolding = holding;
            hasPending = true;
        }

        float FindFloor(Vector3 headPos)
        {
            // thân người không có va chạm nên tia đi thẳng xuống sàn dưới chân người đó
            if (Physics.Raycast(headPos, Vector3.down, out var hit, 2.4f, ~0, QueryTriggerInteraction.Ignore)
                && headPos.y - hit.point.y > 0.9f)
                return hit.point.y;
            return headPos.y - EyeHeight;
        }

        // Sau khi Animator đặt tư thế: bám sàn, nghiêng người, quay đầu, với tay, xoay cổ tay, nắm ngón
        void LateUpdate()
        {
            if (!hasPending) return;
            // ngoài khung nhìn Animator không đặt lại tư thế (CullUpdateTransforms): chỉnh tiếp sẽ cộng dồn
            bool seen = false;
            foreach (var r in renderers) if (r != null && r.isVisible) { seen = true; break; }
            if (!seen) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var t = transform;

            // Bám sàn: chuyển động quay cho người khác tỉ lệ (hoặc nhập sai mốc độ cao) làm chân lơ lửng / lún.
            // Dời hông cho bàn chân thấp nhất chạm sàn; Animator đặt lại hông mỗi khung nên không cộng dồn.
            if (hips != null && footL != null && footR != null)
            {
                float low = Mathf.Min(footL.position.y, footR.position.y) - ankleHeight;
                hips.position += Vector3.up * Mathf.Clamp(t.position.y - low, -1f, 1f);
            }

            if (spine != null && (Mathf.Abs(lean) > 0.05f || Mathf.Abs(forwardLean) > 0.05f))
                spine.rotation = Quaternion.AngleAxis(lean, t.forward) * Quaternion.AngleAxis(forwardLean, t.right) * spine.rotation;

            LookAt(pendingLook);

            if (upperArm != null && lowerArm != null && handBone != null && armReach > 0.05f)
            {
                float dist = (pendingHand - upperArm.position).magnitude;
                // đang cầm vật: với tới vật; tay rỗng: chỉ với khi bàn tay ảo ở gần người (đang thao tác)
                // (bàn tay ảo cách mắt tối đa ~1 m, tức ~2,2 lần sải tay tính từ vai: tới đó vẫn duỗi đủ)
                float want = pendingHolding ? Mathf.Clamp01((armReach * 2.6f - dist) / (armReach * 0.4f))
                                            : Mathf.Clamp01((armReach * 1.15f - dist) / (armReach * 0.2f));
                reachWeight = Mathf.Lerp(reachWeight, want, 1f - Mathf.Exp(-8f * dt));
                if (reachWeight > 0.001f)
                {
                    // khuỷu chúc xuống, hơi ra ngoài và ra sau
                    float s = t.localScale.y;
                    Vector3 hint = upperArm.position + (t.right * 0.25f - t.up * 0.35f - t.forward * 0.1f) * s;
                    TwoBoneIK(upperArm, lowerArm, handBone, pendingHand, hint, reachWeight);
                    AlignWrist(reachWeight);
                }
            }

            grip = Mathf.Lerp(grip, pendingHolding ? 1f : 0f, 1f - Mathf.Exp(-10f * dt));
            if (grip > 0.01f && handBone != null && armReach > 0.05f) CurlFingers(grip);
        }

        /// <summary>
        /// Xoay bàn tay theo bàn tay ảo của người đó (ngón tay = +z, mu = +y). Phần lật quanh trục cẳng tay chia nửa cho
        /// cẳng tay (như người thật xoay cả cẳng tay), nửa còn lại cho cổ tay, cổ tay không vặn quá 80°.
        /// </summary>
        void AlignWrist(float weight)
        {
            var axes = Quaternion.LookRotation(handFingerLocal, handBackLocal);
            var want = pendingHandRot * Quaternion.Inverse(axes);
            Vector3 fore = handBone.position - lowerArm.position;
            if (fore.sqrMagnitude > 1e-8f)
            {
                var delta = want * Quaternion.Inverse(handBone.rotation);
                if (delta.w < 0f) delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w);
                Vector3 p = Vector3.Project(new Vector3(delta.x, delta.y, delta.z), fore.normalized);
                var twist = new Quaternion(p.x, p.y, p.z, delta.w);
                float n = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
                if (n > 1e-6f)
                {
                    twist = new Quaternion(twist.x / n, twist.y / n, twist.z / n, twist.w / n);
                    // xoay quanh trục đi qua khuỷu và cổ tay: bàn tay không dời chỗ
                    lowerArm.rotation = Quaternion.Slerp(Quaternion.identity, twist, 0.5f * weight) * lowerArm.rotation;
                }
            }
            var limited = Quaternion.RotateTowards(handBone.rotation, want, 80f);
            handBone.rotation = Quaternion.Slerp(handBone.rotation, limited, weight);
        }

        /// <summary>Gập các đốt ngón quanh trục ngang bàn tay (nắm cốc); ngón cái khép vào lòng bàn tay.</summary>
        void CurlFingers(float amount)
        {
            Vector3 axis = handBone.rotation * curlAxisLocal;
            Vector3 along = handBone.rotation * handFingerLocal;
            float[] deg = { 38f, 45f, 28f };   // cộng thêm vào độ cong sẵn có của chuyển động: vừa ôm quanh cốc ~7 cm
            for (int f = 1; f < 5; f++)
                for (int j = 0; j < 3; j++)
                {
                    var b = fingers[f][j];
                    if (b != null) b.rotation = Quaternion.AngleAxis(deg[j] * amount, axis) * b.rotation;
                }
            var th = fingers[0];
            if (th[0] != null) th[0].rotation = Quaternion.AngleAxis(28f * amount, along) * th[0].rotation;
            if (th[1] != null) th[1].rotation = Quaternion.AngleAxis(22f * amount, axis) * th[1].rotation;
            if (th[2] != null) th[2].rotation = Quaternion.AngleAxis(18f * amount, axis) * th[2].rotation;
        }

        void LookAt(Vector3 lookFwd)
        {
            if (head == null || lookFwd.sqrMagnitude < 1e-6f) return;
            // không vặn cổ quá 75° so với thân
            Vector3 body = transform.forward;
            float ang = Vector3.Angle(body, lookFwd);
            if (ang > 75f) lookFwd = Vector3.Slerp(body, lookFwd, 75f / ang);
            if (neck != null)
            {
                Vector3 nf = neck.rotation * neckFwdLocal;
                neck.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(nf, lookFwd), 0.4f) * neck.rotation;
            }
            Vector3 hf = head.rotation * headFwdLocal;
            head.rotation = Quaternion.FromToRotation(hf, lookFwd) * head.rotation;
        }

        /// <summary>IK hai xương (vai - khuỷu - cổ tay), không phụ thuộc trục xương của từng file nhân vật.</summary>
        public static void TwoBoneIK(Transform root, Transform mid, Transform tip, Vector3 target, Vector3 hint, float weight)
        {
            Vector3 a = root.position, b = mid.position, c = tip.position;
            Vector3 t = Vector3.Lerp(c, target, weight);
            Vector3 ab = b - a, bc = c - b, ac = c - a, at = t - a;
            float abLen = ab.magnitude, bcLen = bc.magnitude;
            if (abLen < 1e-4f || bcLen < 1e-4f) return;

            float oldAngle = TriangleAngle(ac.magnitude, abLen, bcLen);
            float newAngle = TriangleAngle(at.magnitude, abLen, bcLen);
            Vector3 axis = Vector3.Cross(ab, bc);
            if (axis.sqrMagnitude < 1e-8f)
            {
                axis = Vector3.Cross(hint - a, bc);
                if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(at, bc);
                if (axis.sqrMagnitude < 1e-8f) axis = Vector3.up;
            }
            axis.Normalize();
            mid.rotation = Quaternion.AngleAxis((oldAngle - newAngle) * Mathf.Rad2Deg, axis) * mid.rotation;

            ac = tip.position - a;
            if (ac.sqrMagnitude > 1e-8f && at.sqrMagnitude > 1e-8f)
                root.rotation = Quaternion.FromToRotation(ac, at) * root.rotation;

            // xoay cả cánh tay quanh trục vai - cổ tay cho khuỷu chỉ về phía điểm gợi ý
            ac = tip.position - a;
            float acSq = ac.sqrMagnitude;
            if (acSq > 1e-8f)
            {
                Vector3 n = ac / Mathf.Sqrt(acSq);
                Vector3 abNow = mid.position - a, ah = hint - a;
                Vector3 abProj = abNow - n * Vector3.Dot(abNow, n);
                Vector3 ahProj = ah - n * Vector3.Dot(ah, n);
                float maxReach = abLen + bcLen;
                if (abProj.sqrMagnitude > maxReach * maxReach * 0.001f && ahProj.sqrMagnitude > 1e-8f)
                {
                    var hintRot = Quaternion.FromToRotation(abProj, ahProj);
                    root.rotation = Quaternion.Slerp(Quaternion.identity, hintRot, weight) * root.rotation;
                }
            }
        }

        static float TriangleAngle(float opposite, float side1, float side2)
        {
            float c = Mathf.Clamp((side1 * side1 + side2 * side2 - opposite * opposite) / (2f * side1 * side2), -1f, 1f);
            return Mathf.Acos(c);
        }

        static readonly Dictionary<Material, Material> cutoutCopies = new Dictionary<Material, Material>();

        /// <summary>Tóc, lông mi, lông mày là các tấm mỏng có vùng trong suốt.</summary>
        static bool IsHairCards(string meshName)
        {
            string n = meshName.ToLowerInvariant();
            return n.Contains("hair") || n.Contains("lash") || n.Contains("brow") || n.Contains("beard");
        }

        /// <summary>Bật cắt theo độ trong suốt và vẽ cả hai mặt (không lộ tấm tóc), cho URP Lit và Standard.</summary>
        static Material[] CutoutMaterials(Material[] mats)
        {
            var result = (Material[])mats.Clone();
            for (int i = 0; i < result.Length; i++)
            {
                var m = result[i];
                if (m == null) continue;
                if (!cutoutCopies.TryGetValue(m, out var copy) || copy == null)
                {
                    copy = new Material(m) { name = m.name + " (cắt)", hideFlags = HideFlags.DontSave };
                    if (copy.HasProperty("_AlphaClip")) copy.SetFloat("_AlphaClip", 1f);   // URP Lit
                    if (copy.HasProperty("_Mode")) copy.SetFloat("_Mode", 1f);             // Standard: Cutout
                    if (copy.HasProperty("_Cutoff")) copy.SetFloat("_Cutoff", 0.4f);
                    if (copy.HasProperty("_Cull")) copy.SetFloat("_Cull", 0f);
                    copy.EnableKeyword("_ALPHATEST_ON");
                    copy.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    cutoutCopies[m] = copy;
                }
                result[i] = copy;
            }
            return result;
        }

        /// <summary>Vật liệu Standard của file FBX hiện hồng trong URP: đổi sang URP Lit, giữ ảnh và màu.</summary>
        static Material[] FixMaterials(Material[] mats)
        {
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp == null) return mats;
            var result = (Material[])mats.Clone();
            for (int i = 0; i < result.Length; i++)
            {
                var m = result[i];
                if (m == null) continue;
                string s = m.shader != null ? m.shader.name : "";
                bool legacy = m.shader == null || !m.shader.isSupported || s == "Standard" || s == "Standard (Specular setup)"
                              || s.StartsWith("Legacy Shaders/") || s == "Autodesk Interactive" || s == "Hidden/InternalErrorShader";
                if (!legacy) continue;
                if (!urpCopies.TryGetValue(m, out var copy) || copy == null)
                {
                    copy = new Material(urp) { name = m.name + " (URP)", hideFlags = HideFlags.DontSave };
                    if (m.HasProperty("_MainTex") && m.mainTexture != null) copy.SetTexture("_BaseMap", m.mainTexture);
                    if (m.HasProperty("_Color")) copy.SetColor("_BaseColor", m.color);
                    if (m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") != null)
                    {
                        copy.SetTexture("_BumpMap", m.GetTexture("_BumpMap"));
                        copy.EnableKeyword("_NORMALMAP");
                    }
                    copy.SetFloat("_Smoothness", 0.3f);
                    urpCopies[m] = copy;
                }
                result[i] = copy;
            }
            return result;
        }
    }
}
