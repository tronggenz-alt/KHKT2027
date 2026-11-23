using System;
using System.Collections.Generic;
using System.Text;
using Khkt.Avatar;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Interaction;
using Khkt.Menu;
using Khkt.Rendering;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.Network
{
    /// <summary>
    /// Thấy nhau trong phòng lab: vị trí đầu, tay của từng người và vật đang được cầm. Máy khách gửi cho
    /// máy chủ phòng, máy chủ kiểm tra rồi chuyển cho các máy còn lại. Một vật chỉ một người cầm: máy chủ
    /// quyết định ai được, người đến sau bị từ chối.
    /// Chưa đồng bộ dung dịch, phản ứng, chỗ đặt trong giá: đây là bước thử trước khi làm toàn bộ thí nghiệm.
    /// </summary>
    public class LabPresence : MonoBehaviour
    {
        class Remote
        {
            public string name = "";
            public GameObject root;
            public HumanoidAvatar avatar;
            /// <summary>Người 3D Mixamo (Assets/Lab/NguoiChoi): có file thì thay thân dựng bằng mã, bảng tên vẫn của HumanoidAvatar.</summary>
            public RemoteAvatar person;
            public Vector3 headPos, handPos;
            public float yaw, pitch;
            public Quaternion handRot = Quaternion.identity;
            public Vector3 dispHead, dispHand;
            public float dispYaw, dispPitch;
            public Quaternion dispHandRot = Quaternion.identity;
            public bool seen;
            public bool isTeacher;
        }

        struct Pose
        {
            public Vector3 pos;
            public Quaternion rot;
        }

        public static LabPresence Instance { get; private set; }

        const string MsgPose = "vera.vitri", MsgHello = "vera.chao", MsgName = "vera.ten", MsgGone = "vera.roi";
        const string MsgAsk = "vera.xin_cam", MsgOwner = "vera.cam", MsgDenied = "vera.tu_choi", MsgObject = "vera.vat", MsgRelease = "vera.tha";
        const float SendInterval = 1f / 15f;
        const ulong NoOwner = ulong.MaxValue;

        public int RemoteCount => remotes.Count;
        public long PayloadBytesSent { get; private set; }
        public long PayloadBytesReceived { get; private set; }

        readonly Dictionary<ulong, Remote> remotes = new Dictionary<ulong, Remote>();
        readonly Dictionary<ulong, string> names = new Dictionary<ulong, string>();
        readonly Dictionary<int, Grabbable> byId = new Dictionary<int, Grabbable>();
        readonly Dictionary<Grabbable, int> idOf = new Dictionary<Grabbable, int>();
        readonly Dictionary<int, ulong> owners = new Dictionary<int, ulong>();
        readonly Dictionary<int, Pose> lastPose = new Dictionary<int, Pose>();
        readonly HashSet<int> remoteHeld = new HashSet<int>();

        NetworkManager net;
        CustomMessagingManager msg;
        RoomConnection room;
        DesktopViewer viewer;
        HandController hand;
        Grabbable localHeld;
        int localHeldId;
        float nextSend, nextHello, nextHud;
        bool welcomed;
        Text hud;

        ulong Me => net != null ? net.LocalClientId : 0;

        // =====================================================================
        // Vòng đời
        // =====================================================================

        void Start()
        {
            Instance = this;
            viewer = FindAnyObjectByType<DesktopViewer>();
            hand = HandController.Active != null ? HandController.Active : FindAnyObjectByType<HandController>();
            net = NetworkManager.Singleton;
            if (net == null || !net.IsListening) return;

            room = RoomConnection.Instance;
            msg = net.CustomMessagingManager;

            msg.RegisterNamedMessageHandler(MsgPose, OnPose);
            msg.RegisterNamedMessageHandler(MsgHello, OnHello);
            msg.RegisterNamedMessageHandler(MsgName, OnName);
            msg.RegisterNamedMessageHandler(MsgGone, OnGone);
            msg.RegisterNamedMessageHandler(MsgAsk, OnAsk);
            msg.RegisterNamedMessageHandler(MsgOwner, OnOwner);
            msg.RegisterNamedMessageHandler(MsgDenied, OnDenied);
            msg.RegisterNamedMessageHandler(MsgObject, OnObject);
            msg.RegisterNamedMessageHandler(MsgRelease, OnRelease);
            ZoneStreamer.ZoneChanged += OnZoneChanged;
            if (room != null) room.Lost += OnRoomLost;
            if (net.IsServer) net.OnClientDisconnectCallback += OnClientLeft;

            names[Me] = GameSession.DisplayName;
            welcomed = net.IsServer;
            ScanObjects();
            BuildHud();
            LabMessages.Say(net.IsServer ? $"Đã mở phòng. Mã vào phòng: {room?.Code}" : "Đã vào phòng học chung", LabMessages.Tone.Info);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ZoneStreamer.ZoneChanged -= OnZoneChanged;
            if (room != null) room.Lost -= OnRoomLost;
            if (net != null) net.OnClientDisconnectCallback -= OnClientLeft;
            if (msg != null && net != null && net.IsListening)
            {
                foreach (var name in new[] { MsgPose, MsgHello, MsgName, MsgGone, MsgAsk, MsgOwner, MsgDenied, MsgObject, MsgRelease })
                    msg.UnregisterNamedMessageHandler(name);
            }
            foreach (var r in remotes.Values) if (r.root != null) Destroy(r.root);
            if (hud != null) Destroy(hud.transform.parent.gameObject);
        }

        void OnRoomLost()
        {
            LabMessages.Say("Máy mở phòng đã đóng phòng: bạn vẫn làm tiếp một mình được", LabMessages.Tone.Warn);
            // trả lại những vật đang bị khóa vì người khác cầm
            foreach (int id in remoteHeld)
                if (byId.TryGetValue(id, out var g) && g != null) g.SetHeld(false);
            remoteHeld.Clear();
            Destroy(gameObject);
        }

        void Update()
        {
            if (net == null || !net.IsListening)
            {
                MoveRemotes();
                return;
            }
            float now = Time.realtimeSinceStartup;
            if (!welcomed && now >= nextHello)
            {
                // máy chủ có thể chưa tải xong lab: chào lại tới khi được trả lời
                nextHello = now + 1f;
                SendName();
            }
            WatchHeld();
            if (now >= nextSend)
            {
                nextSend = now + SendInterval;
                SendPose();
                if (localHeld != null && localHeldId != 0) SendObject(localHeldId, localHeld.transform);
            }
            MoveRemotes();
            MoveRemoteObjects();
            if (now >= nextHud)
            {
                nextHud = now + 0.5f;
                RefreshHud();
            }
        }

        // =====================================================================
        // Gửi, nhận chung
        // =====================================================================

        void ToServer(string name, FastBufferWriter w, NetworkDelivery d)
        {
            PayloadBytesSent += w.Length;
            msg.SendNamedMessage(name, NetworkManager.ServerClientId, w, d);
        }

        /// <summary>Máy chủ gửi cho mọi máy khách, trừ người vừa gửi tin gốc.</summary>
        void ToClients(string name, FastBufferWriter w, NetworkDelivery d, ulong except)
        {
            foreach (ulong id in net.ConnectedClientsIds)
            {
                if (id == NetworkManager.ServerClientId || id == except) continue;
                PayloadBytesSent += w.Length;
                msg.SendNamedMessage(name, id, w, d);
            }
        }

        static FastBufferWriter NewWriter(int size = 96) => new FastBufferWriter(size, Allocator.Temp, 1024);

        // =====================================================================
        // Người trong phòng
        // =====================================================================

        void SendName()
        {
            using (var w = NewWriter())
            {
                w.WriteValueSafe(Me);
                w.WriteValueSafe(GameSession.DisplayName);
                ToServer(MsgName, w, NetworkDelivery.ReliableSequenced);
            }
        }

        void OnName(ulong sender, FastBufferReader r)
        {
            PayloadBytesReceived += r.Length;
            r.ReadValueSafe(out ulong id);
            r.ReadValueSafe(out string name);
            if (net.IsServer)
            {
                if (id != sender) return;
                bool first = !names.ContainsKey(id);
                names[id] = name;
                if (first)
                {
                    LabMessages.Say($"{name} đã vào phòng", LabMessages.Tone.Info);
                    using (var w = NewWriter())
                    {
                        w.WriteValueSafe(id);
                        w.WriteValueSafe(name);
                        ToClients(MsgName, w, NetworkDelivery.ReliableSequenced, id);
                    }
                }
                SendWorld(id);
                return;
            }
            names[id] = name;
            if (remotes.TryGetValue(id, out var remote)) SetName(remote, name);
        }

        /// <summary>Người đến sau: tên mọi người, ai đang cầm gì, vật đã bị dời tới đâu.</summary>
        void SendWorld(ulong to)
        {
            foreach (var kv in names)
            {
                if (kv.Key == to) continue;
                using (var w = NewWriter())
                {
                    w.WriteValueSafe(kv.Key);
                    w.WriteValueSafe(kv.Value);
                    PayloadBytesSent += w.Length;
                    msg.SendNamedMessage(MsgName, to, w, NetworkDelivery.ReliableSequenced);
                }
            }
            foreach (var kv in lastPose)
            {
                if (owners.ContainsKey(kv.Key)) continue;
                using (var w = NewWriter())
                {
                    WriteObject(w, Me, kv.Key, kv.Value.pos, kv.Value.rot);
                    PayloadBytesSent += w.Length;
                    msg.SendNamedMessage(MsgRelease, to, w, NetworkDelivery.ReliableSequenced);
                }
            }
            foreach (var kv in owners)
            {
                using (var w = NewWriter())
                {
                    w.WriteValueSafe(kv.Key);
                    w.WriteValueSafe(kv.Value);
                    PayloadBytesSent += w.Length;
                    msg.SendNamedMessage(MsgOwner, to, w, NetworkDelivery.ReliableSequenced);
                }
            }
            using (var w = NewWriter())
            {
                w.WriteValueSafe(true);
                PayloadBytesSent += w.Length;
                msg.SendNamedMessage(MsgHello, to, w, NetworkDelivery.ReliableSequenced);
            }
        }

        void OnHello(ulong sender, FastBufferReader r)
        {
            PayloadBytesReceived += r.Length;
            welcomed = true;
        }

        void OnGone(ulong sender, FastBufferReader r)
        {
            PayloadBytesReceived += r.Length;
            r.ReadValueSafe(out ulong id);
            RemoveRemote(id);
        }

        void OnClientLeft(ulong id)
        {
            if (id == NetworkManager.ServerClientId) return;
            if (names.TryGetValue(id, out string name)) LabMessages.Say($"{name} đã rời phòng", LabMessages.Tone.Info);
            names.Remove(id);
            RemoveRemote(id);
            using (var w = NewWriter())
            {
                w.WriteValueSafe(id);
                ToClients(MsgGone, w, NetworkDelivery.ReliableSequenced, id);
            }
            // người rời phòng đang cầm gì thì thả tại chỗ cuối cùng
            var held = new List<int>();
            foreach (var kv in owners) if (kv.Value == id) held.Add(kv.Key);
            foreach (int obj in held)
            {
                var t = byId.TryGetValue(obj, out var g) && g != null ? g.transform : null;
                Pose p = lastPose.TryGetValue(obj, out var known) ? known : new Pose { pos = t != null ? t.position : Vector3.zero, rot = t != null ? t.rotation : Quaternion.identity };
                ReleaseOnServer(obj, p.pos, p.rot, Me);
            }
        }

        void RemoveRemote(ulong id)
        {
            if (!remotes.TryGetValue(id, out var r)) return;
            if (r.root != null) Destroy(r.root);
            remotes.Remove(id);
        }

        void SendPose()
        {
            if (viewer == null || viewer.cameraTransform == null) return;
            var cam = viewer.cameraTransform;
            Vector3 handPos = hand != null && hand.hand != null ? hand.hand.transform.position : cam.position + cam.forward * 0.4f;
            Quaternion handRot = hand != null && hand.hand != null ? hand.hand.transform.rotation : cam.rotation;
            float yaw = viewer.transform.eulerAngles.y, pitch = viewer.Pitch;
            using (var w = NewWriter())
            {
                WritePose(w, Me, cam.position, yaw, pitch, handPos, handRot);
                if (net.IsServer) ToClients(MsgPose, w, NetworkDelivery.UnreliableSequenced, Me);
                else ToServer(MsgPose, w, NetworkDelivery.UnreliableSequenced);
            }
        }

        static void WritePose(FastBufferWriter w, ulong who, Vector3 head, float yaw, float pitch, Vector3 handPos, Quaternion handRot)
        {
            w.WriteValueSafe(who);
            w.WriteValueSafe(head);
            w.WriteValueSafe(yaw);
            w.WriteValueSafe(pitch);
            w.WriteValueSafe(handPos);
            w.WriteValueSafe(handRot);
        }

        void OnPose(ulong sender, FastBufferReader r)
        {
            PayloadBytesReceived += r.Length;
            r.ReadValueSafe(out ulong who);
            r.ReadValueSafe(out Vector3 head);
            r.ReadValueSafe(out float yaw);
            r.ReadValueSafe(out float pitch);
            r.ReadValueSafe(out Vector3 handPos);
            r.ReadValueSafe(out Quaternion handRot);
            if (net.IsServer)
            {
                if (who != sender) return;
                using (var w = NewWriter())
                {
                    WritePose(w, who, head, yaw, pitch, handPos, handRot);
                    ToClients(MsgPose, w, NetworkDelivery.UnreliableSequenced, who);
                }
            }
            if (who == Me) return;
            if (!remotes.TryGetValue(who, out var remote))
            {
                remote = MakeRemote(who);
                remotes[who] = remote;
            }
            remote.headPos = head;
            remote.yaw = yaw;
            remote.pitch = pitch;
            remote.handPos = handPos;
            remote.handRot = handRot;
            if (!remote.seen)
            {
                remote.seen = true;
                remote.dispHead = head;
                remote.dispYaw = yaw;
                remote.dispPitch = pitch;
                remote.dispHand = handPos;
                remote.dispHandRot = handRot;
                Transform camT = viewer != null ? viewer.cameraTransform : null;
                remote.avatar.ApplyPose(head, yaw, pitch, handPos, handRot, null, camT);
            }
        }

        void MoveRemotes()
        {
            float k = 1f - Mathf.Exp(-14f * Time.deltaTime);
            Transform camT = viewer != null ? viewer.cameraTransform : (Camera.main != null ? Camera.main.transform : null);
            foreach (var kv in remotes)
            {
                var r = kv.Value;
                if (r.root == null || r.avatar == null) continue;
                // dịch chuyển trạm thì nhảy luôn, không trôi xuyên tường
                bool snap = (r.dispHead - r.headPos).sqrMagnitude > 9f;
                r.dispHead = snap ? r.headPos : Vector3.Lerp(r.dispHead, r.headPos, k);
                r.dispYaw = snap ? r.yaw : Mathf.LerpAngle(r.dispYaw, r.yaw, k);
                r.dispPitch = snap ? r.pitch : Mathf.LerpAngle(r.dispPitch, r.pitch, k);
                r.dispHand = snap ? r.handPos : Vector3.Lerp(r.dispHand, r.handPos, k);
                r.dispHandRot = snap ? r.handRot : Quaternion.Slerp(r.dispHandRot, r.handRot, k);

                r.avatar.ApplyPose(r.dispHead, r.dispYaw, r.dispPitch, r.dispHand, r.dispHandRot, null, camT);
                if (camT != null) r.avatar.UpdateLod(camT.position);
                if (r.person != null)
                {
                    r.person.Drive(r.dispHead, Quaternion.Euler(r.dispPitch, r.dispYaw, 0), r.dispHand, r.dispHandRot, IsHolding(kv.Key));
                    var plate = r.avatar.NameplateTransform;
                    if (plate != null)
                    {
                        plate.position = r.person.HeadTop + Vector3.up * 0.14f;
                        if (camT != null) plate.rotation = Quaternion.LookRotation(plate.position - camT.position);
                    }
                }
            }
        }

        // =====================================================================
        // Vật đang cầm
        // =====================================================================

        /// <summary>Mã vật: đường dẫn trong cảnh lúc mới tải, giống nhau trên mọi máy vì cùng một bản dựng.</summary>
        void ScanObjects()
        {
            foreach (var g in ZoneRoot.FindAll<Grabbable>())
            {
                if (g == null || idOf.ContainsKey(g)) continue;
                int id = StableId(g.transform);
                if (byId.TryGetValue(id, out var old) && old != null && old != g) continue;
                byId[id] = g;
                idOf[g] = id;
                if (lastPose.TryGetValue(id, out var p)) g.transform.SetPositionAndRotation(p.pos, p.rot);
                if (owners.TryGetValue(id, out ulong owner) && owner != Me) MarkRemoteHeld(id, g);
            }
        }

        static int StableId(Transform t)
        {
            var sb = new StringBuilder(t.gameObject.scene.name);
            var chain = new List<Transform>();
            for (var p = t; p != null; p = p.parent) chain.Add(p);
            for (int i = chain.Count - 1; i >= 0; i--) sb.Append('/').Append(chain[i].GetSiblingIndex());
            sb.Append('/').Append(t.name);
            // FNV-1a: hash của string đổi theo tiến trình nên không dùng GetHashCode
            uint h = 2166136261;
            foreach (char c in sb.ToString()) h = (h ^ c) * 16777619;
            return (int)(h | 1);
        }

        void OnZoneChanged(int zone, bool loaded)
        {
            if (loaded) ScanObjects();
        }

        void WatchHeld()
        {
            var now = hand != null ? hand.Held : null;
            if (now == localHeld) return;
            if (localHeld != null && localHeldId != 0) SendRelease(localHeldId, localHeld.transform);
            localHeld = now;
            localHeldId = 0;
            if (now == null) return;
            if (!idOf.TryGetValue(now, out localHeldId)) ScanObjects();
            if (!idOf.TryGetValue(now, out localHeldId)) return;  // vật sinh lúc chạy: chưa đồng bộ
            if (net.IsServer) AskOnServer(localHeldId, Me);
            else
            {
                using (var w = NewWriter())
                {
                    w.WriteValueSafe(localHeldId);
                    ToServer(MsgAsk, w, NetworkDelivery.ReliableSequenced);
                }
            }
        }

        void OnAsk(ulong sender, FastBufferReader r)
        {
            PayloadBytesReceived += r.Length;
            r.ReadValueSafe(out int id);
            if (net.IsServer) AskOnServer(id, sender);
        }

        void AskOnServer(int id, ulong who)
        {
            if (owners.TryGetValue(id, out ulong owner) && owner != who)
            {
                if (who == Me) Denied(id);
                else
                {
                    using (var w = NewWriter())
                    {
                        w.WriteValueSafe(id);
                        PayloadBytesSent += w.Length;
                        msg.SendNamedMessage(MsgDenied, who, w, NetworkDelivery.ReliableSequenced);
                    }
                }
                return;
            }
            ApplyOwner(id, who);
            using (var w = NewWriter())
            {
                w.WriteValueSafe(id);
                w.WriteValueSafe(who);
                ToClients(MsgOwner, w, NetworkDelivery.ReliableSequenced, ulong.MaxValue);
            }
        }

        void OnOwner(ulong sender, FastBufferReader r)
        {
            PayloadBytesReceived += r.Length;
            r.ReadValueSafe(out int id);
            r.ReadValueSafe(out ulong who);
            ApplyOwner(id, who);
        }

        void ApplyOwner(int id, ulong who)
        {
            owners[id] = who;
            if (who != Me && byId.TryGetValue(id, out var g) && g != null) MarkRemoteHeld(id, g);
        }

        void MarkRemoteHeld(int id, Grabbable g)
        {
            // SetHeld tắt va chạm: người khác đang cầm thì tia của mình xuyên qua, không chọn được
            if (remoteHeld.Add(id) && !g.IsHeld) g.SetHeld(true);
        }

        void OnDenied(ulong sender, FastBufferReader r)
        {
            PayloadBytesReceived += r.Length;
            r.ReadValueSafe(out int id);
            Denied(id);
        }

        void Denied(int id)
        {
            LabMessages.Say("Bạn khác đang cầm vật này", LabMessages.Tone.Warn);
            if (localHeldId != id || localHeld == null) return;
            var g = localHeld;
            localHeld = null;
            localHeldId = 0;
            hand.ReturnHome();
            if (owners.TryGetValue(id, out ulong owner) && owner != Me)
            {
                remoteHeld.Remove(id);
                MarkRemoteHeld(id, g);
            }
        }

        static void WriteObject(FastBufferWriter w, ulong who, int id, Vector3 pos, Quaternion rot)
        {
            w.WriteValueSafe(who);
            w.WriteValueSafe(id);
            w.WriteValueSafe(pos);
            w.WriteValueSafe(rot);
        }

        void SendObject(int id, Transform t)
        {
            if (owners.TryGetValue(id, out ulong owner) && owner != Me) return;
            using (var w = NewWriter())
            {
                WriteObject(w, Me, id, t.position, t.rotation);
                if (net.IsServer) ToClients(MsgObject, w, NetworkDelivery.UnreliableSequenced, Me);
                else ToServer(MsgObject, w, NetworkDelivery.UnreliableSequenced);
            }
        }

        void OnObject(ulong sender, FastBufferReader r)
        {
            PayloadBytesReceived += r.Length;
            r.ReadValueSafe(out ulong who);
            r.ReadValueSafe(out int id);
            r.ReadValueSafe(out Vector3 pos);
            r.ReadValueSafe(out Quaternion rot);
            if (net.IsServer)
            {
                // chỉ người được máy chủ cho cầm mới được dời vật
                if (who != sender || !owners.TryGetValue(id, out ulong owner) || owner != who) return;
                using (var w = NewWriter())
                {
                    WriteObject(w, who, id, pos, rot);
                    ToClients(MsgObject, w, NetworkDelivery.UnreliableSequenced, who);
                }
            }
            if (who != Me) lastPose[id] = new Pose { pos = pos, rot = rot };
        }

        void MoveRemoteObjects()
        {
            float k = 1f - Mathf.Exp(-14f * Time.deltaTime);
            foreach (int id in remoteHeld)
            {
                if (!byId.TryGetValue(id, out var g) || g == null || !lastPose.TryGetValue(id, out var p)) continue;
                var t = g.transform;
                t.SetPositionAndRotation(Vector3.Lerp(t.position, p.pos, k), Quaternion.Slerp(t.rotation, p.rot, k));
            }
        }

        void SendRelease(int id, Transform t)
        {
            lastPose[id] = new Pose { pos = t.position, rot = t.rotation };
            if (net.IsServer)
            {
                ReleaseOnServer(id, t.position, t.rotation, Me);
                return;
            }
            owners.Remove(id);
            using (var w = NewWriter())
            {
                WriteObject(w, Me, id, t.position, t.rotation);
                ToServer(MsgRelease, w, NetworkDelivery.ReliableSequenced);
            }
        }

        void OnRelease(ulong sender, FastBufferReader r)
        {
            PayloadBytesReceived += r.Length;
            r.ReadValueSafe(out ulong who);
            r.ReadValueSafe(out int id);
            r.ReadValueSafe(out Vector3 pos);
            r.ReadValueSafe(out Quaternion rot);
            if (net.IsServer)
            {
                if (who != sender || !owners.TryGetValue(id, out ulong owner) || owner != who) return;
                ReleaseOnServer(id, pos, rot, who);
                return;
            }
            ApplyRelease(id, pos, rot, who);
        }

        void ReleaseOnServer(int id, Vector3 pos, Quaternion rot, ulong who)
        {
            ApplyRelease(id, pos, rot, who);
            using (var w = NewWriter())
            {
                WriteObject(w, who, id, pos, rot);
                ToClients(MsgRelease, w, NetworkDelivery.ReliableSequenced, who == Me ? ulong.MaxValue : who);
            }
        }

        void ApplyRelease(int id, Vector3 pos, Quaternion rot, ulong who)
        {
            owners.Remove(id);
            lastPose[id] = new Pose { pos = pos, rot = rot };
            if (who == Me) return;
            if (!byId.TryGetValue(id, out var g) || g == null) return;
            if (remoteHeld.Remove(id)) g.SetHeld(false);
            g.transform.SetPositionAndRotation(pos, rot);
        }

        // =====================================================================
        // Hình người, bảng trạng thái
        // =====================================================================

        Remote MakeRemote(ulong id, bool? forceTeacher = null)
        {
            string displayName = names.TryGetValue(id, out string found) ? found : "Bạn học";
            bool isTeacher = forceTeacher ?? IsTeacherRole(id, displayName);

            var root = new GameObject("Người cùng phòng " + id);
            var avatar = root.AddComponent<HumanoidAvatar>();
            avatar.Initialize(displayName, isTeacher);
            // Máy có file nhân vật Mixamo: người 3D đó thay thân dựng bằng mã (ẩn đi, chỉ giữ bảng tên)
            var person = RemoteAvatar.Create(root.transform, "Thân người 3D");
            if (person != null)
                foreach (var rend in avatar.GetComponentsInChildren<Renderer>(true))
                    if (!rend.transform.IsChildOf(person.transform)) rend.enabled = false;

            return new Remote
            {
                name = displayName,
                root = root,
                avatar = avatar,
                person = person,
                isTeacher = isTeacher
            };
        }

        static bool IsTeacherRole(ulong id, string name)
        {
            if (id == NetworkManager.ServerClientId) return true;
            if (string.IsNullOrEmpty(name)) return false;
            return name.StartsWith("GV", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Giáo viên", StringComparison.OrdinalIgnoreCase);
        }

        static void SetName(Remote r, string name)
        {
            r.name = name;
            r.isTeacher = r.isTeacher || IsTeacherRole(ulong.MaxValue, name);
            if (r.avatar != null) r.avatar.SetName(name, r.isTeacher);
        }

        /// <summary>Tạo nhanh một người chơi mô phỏng (chế độ thử 1 máy) qua chính LabPresence & HumanoidAvatar.</summary>
        public HumanoidAvatar SpawnSimulatedRemote(ulong fakeId, string displayName, bool isTeacher,
            Vector3 headPos, float yaw, float pitch, Vector3 handPos, Quaternion handRot)
        {
            names[fakeId] = displayName;
            if (!remotes.TryGetValue(fakeId, out var r) || r.root == null)
            {
                r = MakeRemote(fakeId, isTeacher);
                remotes[fakeId] = r;
            }
            r.isTeacher = isTeacher;
            r.name = displayName;
            r.headPos = r.dispHead = headPos;
            r.yaw = r.dispYaw = yaw;
            r.pitch = r.dispPitch = pitch;
            r.handPos = r.dispHand = handPos;
            r.handRot = r.dispHandRot = handRot;
            r.seen = true;
            r.avatar.SetName(displayName, isTeacher);

            Transform camT = viewer != null ? viewer.cameraTransform : (Camera.main != null ? Camera.main.transform : null);
            r.avatar.ApplyPose(headPos, yaw, pitch, handPos, handRot, null, camT);
            if (r.person != null) r.person.Drive(headPos, Quaternion.Euler(pitch, yaw, 0), handPos, handRot, false);
            if (camT != null) r.avatar.UpdateLod(camT.position);
            return r.avatar;
        }

        bool IsHolding(ulong who)
        {
            foreach (var kv in owners) if (kv.Value == who) return true;
            return false;
        }

        void BuildHud()
        {
            var ui = FindAnyObjectByType<ModernUI>();
            if (ui == null || ui.Canvas == null) return;
            var plate = GlassKit.Panel(ui.Canvas.transform, "Phòng học chung", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(430, 48), null, 3f);
            plate.color = new Color(0.03f, 0.05f, 0.09f, 0.78f);
            plate.sprite = GlassKit.Solid;
            hud = GlassKit.Label(plate.transform, "Chữ", "", 17, Color.white, false, TextAnchor.MiddleLeft, new Vector2(16, 0), new Vector2(404, 48));
            RefreshHud();
        }

        void RefreshHud()
        {
            if (hud == null) return;
            string route = room.ViaRelay ? "Internet" : "LAN";
            hud.text = $"Phòng <b>{room.Code}</b> · {route} · {remotes.Count + 1} người";
        }

        // =====================================================================
        // Cho bài kiểm thử
        // =====================================================================

        public int IdOf(Grabbable g) => g != null && idOf.TryGetValue(g, out int id) ? id : 0;
        public Grabbable ObjectById(int id) => byId.TryGetValue(id, out var g) ? g : null;
        public bool IsRemoteHeld(int id) => remoteHeld.Contains(id);

        public bool RemoteHeadOf(ulong id, out Vector3 pos, out string name)
        {
            pos = Vector3.zero;
            name = "";
            if (!remotes.TryGetValue(id, out var r) || r.root == null) return false;
            pos = r.headPos;
            name = r.name;
            return true;
        }
    }
}
