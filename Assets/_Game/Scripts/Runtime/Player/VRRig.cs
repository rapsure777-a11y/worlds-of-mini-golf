using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Player rig: tracked head and hands, putter in the dominant hand, and comfort locomotion.
    ///
    /// Controls (dominant = putter hand, off = other hand):
    ///   Either stick forward, release  teleport (arc from that hand)
    ///   Either stick left/right        snap turn
    ///   Off-hand grip (hold + pull)    grab-move, Walkabout style
    ///   Dominant grip + stick up/down  putter length
    ///   Dominant grip + stick l/r      putter angle (add trigger: rotate head)
    ///   Dominant A / X                 teleport beside the ball, facing the cup
    ///   Dominant B / Y                 return ball to its last resting spot
    ///   Off-hand B / Y (hold 1 s)      swap putter hand
    ///   Off-hand A / X                 show / hide scorecard
    ///   Menu (hold 1.5 s)              restart hole
    /// Without a headset it falls back to a desktop debug mode (see <see cref="DesktopDebug"/>).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class VRRig : MonoBehaviour, IPlayerMover
    {
        const string PrefLeftHanded = "player.leftHanded";

        [SerializeField] Transform cameraOffset;
        [SerializeField] Camera head;
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightHand;
        [SerializeField] Putter putter;
        [SerializeField] CourseController course;
        [SerializeField] LineRenderer teleportLine;
        [SerializeField] Transform teleportReticle;
        [SerializeField] GameObject scorecard;
        [SerializeField] bool leftHanded;

        [Header("Comfort")]
        [SerializeField] float snapTurnDegrees = 30f;
        [SerializeField] float teleportSpeed = 7f;
        [SerializeField] float maxTeleportSlope = 40f;
        [SerializeField] LayerMask teleportMask = ~0;

        [Header("Putter adjustment")]
        [SerializeField] float lengthSpeed = 0.35f;
        [SerializeField] float angleSpeed = 60f;

        public Camera Head => head;
        public Putter Putter => putter;
        public bool LeftHanded => leftHanded;
        public Transform DominantHand => leftHanded ? leftHand : rightHand;
        public Transform OffHand => leftHanded ? rightHand : leftHand;
        public bool XRActive { get; private set; }

        HandInput m_Left, m_Right;
        HandInput Dom => leftHanded ? m_Left : m_Right;
        HandInput Off => leftHanded ? m_Right : m_Left;

        bool m_TurnArmed = true;
        HandInput m_TeleportHand;
        bool m_TeleportValid;
        Vector3 m_TeleportTarget;
        readonly List<Vector3> m_Arc = new List<Vector3>();
        bool m_Adjusting;
        bool m_Grabbing;
        Vector3 m_GrabPrevLocal;
        float m_SwapHeld, m_MenuHeld;
        DesktopDebug m_Desktop;

        public void Configure(Transform offset, Camera cam, Transform left, Transform right, Putter p,
            CourseController c, LineRenderer line, Transform reticle, GameObject card)
        {
            cameraOffset = offset; head = cam; leftHand = left; rightHand = right; putter = p;
            course = c; teleportLine = line; teleportReticle = reticle; scorecard = card;
        }

        void Awake()
        {
            leftHanded = PlayerPrefs.GetInt(PrefLeftHanded, leftHanded ? 1 : 0) == 1;
            m_Left = new HandInput("LeftHand");
            m_Right = new HandInput("RightHand");

            var tpd = head.GetComponent<TrackedPoseDriver>();
            if (!tpd) tpd = head.gameObject.AddComponent<TrackedPoseDriver>();
            tpd.positionInput = new InputActionProperty(new InputAction(binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            tpd.rotationInput = new InputActionProperty(new InputAction(binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

            if (teleportLine) teleportLine.enabled = false;
            if (teleportReticle) teleportReticle.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            m_Left.Enable(); m_Right.Enable();
            Application.onBeforeRender += OnBeforeRender;
        }

        void OnDisable()
        {
            m_Left.Disable(); m_Right.Disable();
            Application.onBeforeRender -= OnBeforeRender;
        }

        void OnDestroy()
        {
            m_Left?.Dispose(); m_Right?.Dispose();
        }

        void Start()
        {
            XRActive = XRSettings.isDeviceActive;
            if (XRActive)
            {
                var subsystems = new List<XRInputSubsystem>();
                SubsystemManager.GetSubsystems(subsystems);
                foreach (var s in subsystems) s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
            }
            else
            {
                m_Desktop = new DesktopDebug(this, head, cameraOffset);
            }
            ApplyHandedness();
        }

        void ApplyHandedness()
        {
            if (putter)
            {
                putter.Hand = DominantHand;
                putter.ResetTracking();
            }
            foreach (var r in leftHand.GetComponentsInChildren<Renderer>()) r.enabled = leftHand != DominantHand;
            foreach (var r in rightHand.GetComponentsInChildren<Renderer>()) r.enabled = rightHand != DominantHand;
        }

        public void SetLeftHanded(bool value)
        {
            leftHanded = value;
            PlayerPrefs.SetInt(PrefLeftHanded, value ? 1 : 0);
            PlayerPrefs.Save();
            ApplyHandedness();
        }

        void Update()
        {
            if (m_Desktop != null)
            {
                m_Desktop.Update(course);
                return;
            }
            UpdateHandPoses();
            UpdatePutterAdjust();
            UpdateGrabMove();
            UpdateTurnAndTeleport();
            UpdateButtons();
        }

        void OnBeforeRender()
        {
            if (m_Desktop == null) UpdateHandPoses();
            if (putter && putter.Hand) putter.transform.SetPositionAndRotation(putter.Hand.position, putter.Hand.rotation);
        }

        void UpdateHandPoses()
        {
            ApplyPose(m_Left, leftHand);
            ApplyPose(m_Right, rightHand);
        }

        static void ApplyPose(HandInput input, Transform t)
        {
            // controls.Count, not activeControl: activeControl stays null until a value changes.
            if (input.position.controls.Count > 0) t.localPosition = input.position.ReadValue<Vector3>();
            if (input.rotation.controls.Count > 0) t.localRotation = input.rotation.ReadValue<Quaternion>();
        }

        void UpdatePutterAdjust()
        {
            bool adjusting = putter && Dom.Grip;
            if (adjusting)
            {
                Vector2 s = Dom.Stick;
                float dt = Time.deltaTime;
                if (Mathf.Abs(s.y) > 0.2f) putter.AdjustLength(s.y * lengthSpeed * dt);
                if (Mathf.Abs(s.x) > 0.2f)
                {
                    if (Dom.Trigger) putter.AdjustTwist(s.x * angleSpeed * dt);
                    else putter.AdjustAngle(-s.x * angleSpeed * dt);
                }
            }
            else if (m_Adjusting && putter)
            {
                putter.SaveAdjustments();
            }
            m_Adjusting = adjusting;
        }

        void UpdateGrabMove()
        {
            bool grab = Off.Grip;
            Vector3 local = OffHand.localPosition;
            if (grab && m_Grabbing)
            {
                Vector3 delta = cameraOffset.TransformVector(local - m_GrabPrevLocal);
                delta.y = 0f;
                transform.position -= delta;
                SnapToGround();
            }
            m_Grabbing = grab;
            m_GrabPrevLocal = local;
        }

        void UpdateTurnAndTeleport()
        {
            // Teleport: whichever stick is pushed forward first owns the arc.
            if (m_TeleportHand == null)
            {
                foreach (var h in new[] { Off, Dom })
                {
                    if (h == Dom && m_Adjusting) continue;
                    if (h.Stick.y > 0.6f) { m_TeleportHand = h; break; }
                }
            }

            if (m_TeleportHand != null)
            {
                if (m_TeleportHand.Stick.magnitude > 0.3f)
                {
                    UpdateArc(m_TeleportHand);
                }
                else
                {
                    if (m_TeleportValid) TeleportFeet(m_TeleportTarget);
                    m_TeleportHand = null;
                    HideArc();
                }
                return;
            }

            // Snap turn on the off hand, and on the dominant hand when not adjusting the putter.
            float x = Off.Stick.x;
            if (!m_Adjusting && Mathf.Abs(Dom.Stick.x) > Mathf.Abs(x)) x = Dom.Stick.x;
            if (m_TurnArmed && Mathf.Abs(x) > 0.75f)
            {
                transform.RotateAround(head.transform.position, Vector3.up, Mathf.Sign(x) * snapTurnDegrees);
                m_TurnArmed = false;
                putter?.ResetTracking();
            }
            else if (Mathf.Abs(x) < 0.3f) m_TurnArmed = true;
        }

        void UpdateArc(HandInput hand)
        {
            Transform t = hand == m_Left ? leftHand : rightHand;
            Vector3 origin = t.position;
            Vector3 dir = t.forward;
            if (hand.aimRotation.controls.Count > 0)
            {
                origin = cameraOffset.TransformPoint(hand.aimPosition.ReadValue<Vector3>());
                dir = cameraOffset.rotation * (hand.aimRotation.ReadValue<Quaternion>() * Vector3.forward);
            }

            m_Arc.Clear();
            m_TeleportValid = false;
            Vector3 p = origin, v = dir * teleportSpeed;
            const float step = 0.025f;
            m_Arc.Add(p);
            for (int i = 0; i < 120; i++)
            {
                Vector3 next = p + v * step;
                v += Physics.gravity * step;
                if (Physics.Linecast(p, next, out RaycastHit hit, teleportMask, QueryTriggerInteraction.Ignore))
                {
                    m_Arc.Add(hit.point);
                    m_TeleportValid = Vector3.Angle(hit.normal, Vector3.up) <= maxTeleportSlope;
                    m_TeleportTarget = hit.point;
                    break;
                }
                m_Arc.Add(next);
                p = next;
            }

            if (teleportLine)
            {
                teleportLine.enabled = true;
                teleportLine.positionCount = m_Arc.Count;
                teleportLine.SetPositions(m_Arc.ToArray());
                var c = m_TeleportValid ? new Color(0.3f, 0.9f, 1f) : new Color(1f, 0.35f, 0.3f);
                teleportLine.startColor = c; teleportLine.endColor = c;
            }
            if (teleportReticle)
            {
                teleportReticle.gameObject.SetActive(m_TeleportValid);
                teleportReticle.position = m_TeleportTarget + Vector3.up * 0.01f;
            }
        }

        void HideArc()
        {
            m_TeleportValid = false;
            if (teleportLine) teleportLine.enabled = false;
            if (teleportReticle) teleportReticle.gameObject.SetActive(false);
        }

        void UpdateButtons()
        {
            if (Dom.primary.WasPressedThisFrame()) TeleportToBall();
            if (Dom.secondary.WasPressedThisFrame() && HoleController.Active) HoleController.Active.RequestReset();
            if (Off.primary.WasPressedThisFrame() && scorecard) scorecard.SetActive(!scorecard.activeSelf);

            m_SwapHeld = Off.secondary.IsPressed() ? m_SwapHeld + Time.deltaTime : 0f;
            if (m_SwapHeld >= 1f) { m_SwapHeld = float.NegativeInfinity; SetLeftHanded(!leftHanded); }

            bool menu = m_Left.menu.IsPressed() || m_Right.menu.IsPressed();
            m_MenuHeld = menu ? m_MenuHeld + Time.deltaTime : 0f;
            if (m_MenuHeld >= 1.5f) { m_MenuHeld = float.NegativeInfinity; course?.RestartHole(); }
        }

        /// <summary>Stand beside the ball in a putting stance, facing across the line to the cup.</summary>
        public void TeleportToBall()
        {
            var hole = HoleController.Active;
            if (!hole || !hole.Ball) return;
            Vector3 ball = hole.Ball.Position;
            Vector3 toCup = hole.Cup ? hole.Cup.transform.position - ball : head.transform.forward;
            toCup.y = 0f;
            if (toCup.sqrMagnitude < 1e-4f) toCup = Vector3.forward;
            toCup.Normalize();
            Vector3 feet, facing;
            if (m_Desktop != null)
            {
                // Desktop: the putter face follows the camera, so stand behind the ball looking at the cup.
                facing = toCup;
                feet = ball - toCup * 0.9f;
            }
            else
            {
                // Right-handed players stand with the target on their left.
                facing = Quaternion.Euler(0f, leftHanded ? -90f : 90f, 0f) * toCup;
                feet = ball - facing * 0.5f;
            }
            feet.y = ball.y - hole.Ball.Radius;
            TeleportTo(feet, facing);
        }

        public void TeleportTo(Vector3 feetPosition, Vector3 facing)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude > 1e-4f)
            {
                Vector3 headFwd = head.transform.forward; headFwd.y = 0f;
                if (headFwd.sqrMagnitude < 1e-4f) headFwd = transform.forward;
                float angle = Vector3.SignedAngle(headFwd, facing, Vector3.up);
                transform.RotateAround(head.transform.position, Vector3.up, angle);
                m_Desktop?.SetYaw(Quaternion.LookRotation(facing).eulerAngles.y);
            }
            TeleportFeet(feetPosition);
        }

        void TeleportFeet(Vector3 target)
        {
            Vector3 headOffset = head.transform.position - transform.position;
            headOffset.y = 0f;
            transform.position = target - headOffset;
            putter?.ResetTracking();
        }

        void SnapToGround()
        {
            Vector3 from = head.transform.position;
            from.y = transform.position.y + 1f;
            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 3f, teleportMask, QueryTriggerInteraction.Ignore)
                && hit.normal.y > 0.7f)
            {
                var p = transform.position; p.y = hit.point.y; transform.position = p;
            }
        }

        /// <summary>Short buzz on the putter hand, scaled by strike speed.</summary>
        public void Haptic(float amplitude, float duration)
        {
            var node = leftHanded ? XRNode.LeftHand : XRNode.RightHand;
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid) device.SendHapticImpulse(0, Mathf.Clamp01(amplitude), duration);
        }

        void LateUpdate()
        {
            // Keep the scorecard in front of the player when they toggle it on.
            if (scorecard && scorecard.activeSelf && scorecard.transform.parent == null)
            {
                Vector3 fwd = head.transform.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-4f) return;
                fwd.Normalize();
                Vector3 target = head.transform.position + fwd * 1.1f + Vector3.down * 0.25f;
                scorecard.transform.position = Vector3.Lerp(scorecard.transform.position, target, 1f - Mathf.Exp(-4f * Time.deltaTime));
                scorecard.transform.rotation = Quaternion.LookRotation(fwd);
            }
        }

        /// <summary>
        /// Debug controls for inspecting the course without a headset. Not a gameplay mode:
        /// the putter still has to be swung through the ball with the mouse; there is no shoot button.
        /// See <see cref="DesktopHelp"/> for the key list.
        /// </summary>
        class DesktopDebug
        {
            readonly VRRig m_Rig;
            readonly Camera m_Cam;
            float m_Yaw, m_Pitch = 40f, m_Height = 1.6f;

            public DesktopDebug(VRRig rig, Camera cam, Transform offset)
            {
                m_Rig = rig; m_Cam = cam;
                var tpd = cam.GetComponent<TrackedPoseDriver>();
                if (tpd) tpd.enabled = false;
                cam.transform.localPosition = new Vector3(0f, m_Height, 0f);
                m_Yaw = rig.transform.eulerAngles.y;
            }

            public void SyncYaw() => m_Yaw = m_Cam.transform.eulerAngles.y;

            public void SetYaw(float yaw)
            {
                m_Yaw = yaw;
                m_Cam.transform.localRotation = Quaternion.Euler(m_Pitch, m_Yaw - m_Rig.transform.eulerAngles.y, 0f);
            }

            public void Update(CourseController course)
            {
                var kb = Keyboard.current; var mouse = Mouse.current;
                if (kb == null || mouse == null) return;
                float dt = Time.deltaTime;

                if (mouse.rightButton.isPressed)
                {
                    Vector2 d = mouse.delta.ReadValue() * 0.15f;
                    m_Yaw += d.x; m_Pitch = Mathf.Clamp(m_Pitch - d.y, -80f, 85f);
                }
                m_Cam.transform.localRotation = Quaternion.Euler(m_Pitch, m_Yaw - m_Rig.transform.eulerAngles.y, 0f);

                Vector3 move = Vector3.zero;
                if (kb.wKey.isPressed) move += Vector3.forward;
                if (kb.sKey.isPressed) move += Vector3.back;
                if (kb.aKey.isPressed) move += Vector3.left;
                if (kb.dKey.isPressed) move += Vector3.right;
                float speed = kb.leftShiftKey.isPressed ? 6f : 2f;
                if (move != Vector3.zero)
                {
                    move = Quaternion.Euler(0f, m_Yaw, 0f) * move.normalized * speed * dt;
                    m_Rig.transform.position += move;
                    m_Rig.SnapToGround();
                }
                if (kb.eKey.isPressed) m_Height = Mathf.Min(m_Height + dt * 2f, 12f);
                if (kb.qKey.isPressed) m_Height = Mathf.Max(m_Height - dt * 2f, 0.3f);
                m_Cam.transform.localPosition = new Vector3(m_Cam.transform.localPosition.x, m_Height, m_Cam.transform.localPosition.z);

                if (kb.tKey.wasPressedThisFrame) { m_Rig.TeleportToBall(); SyncYaw(); }
                if (kb.rKey.wasPressedThisFrame && HoleController.Active) HoleController.Active.RequestReset();
                if (kb.nKey.wasPressedThisFrame) course?.NextHole();
                if (kb.backspaceKey.wasPressedThisFrame) course?.RestartHole();
                if (course)
                {
                    for (int i = 0; i < 9 && i < course.Holes.Length; i++)
                        if (kb[Key.Digit1 + i].wasPressedThisFrame) { course.StartHole(i, true); SyncYaw(); }
                }

                if (!m_Rig.DesktopMouseControl) return;
                // Putter: hold the left button to put the head down; move the mouse to swing it.
                var putter = m_Rig.putter;
                if (!putter) return;
                Ray ray = m_Cam.ScreenPointToRay(mouse.position.ReadValue());
                var ball = HoleController.Active ? HoleController.Active.Ball : null;
                foreach (var hit in Physics.RaycastAll(ray, 30f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (ball && hit.collider.gameObject == ball.gameObject) continue;
                    Vector3 right = m_Cam.transform.right; right.y = 0f;
                    Vector3 face = Vector3.Cross(Vector3.up, right).normalized; // camera forward, flat
                    float lift = mouse.leftButton.isPressed ? 0.003f : 0.12f;
                    PlaceHead(putter, m_Rig.DominantHand, hit.point + Vector3.up * lift, face);
                    break;
                }
            }
        }

        /// <summary>Pose the hand so the putter head centre sits just above <paramref name="ground"/> with its face toward <paramref name="face"/>.</summary>
        public static void PlaceHead(Putter putter, Transform hand, Vector3 ground, Vector3 face)
        {
            face.y = 0f; face.Normalize();
            Quaternion headPose = Quaternion.LookRotation(Vector3.down, Vector3.Cross(Vector3.down, face));
            // Undo the player's saved shaft angle so the head still lands on the ground.
            hand.rotation = headPose * Quaternion.Inverse(Quaternion.Euler(putter.AngleOffset, 0f, 0f));
            float headHalf = putter.Tuning.headSize.z * 0.5f;
            hand.position = ground + Vector3.up * headHalf + Vector3.up * putter.Length;
        }

        /// <summary>When false, desktop mode leaves the putter hand alone (used by the smoke test).</summary>
        public bool DesktopMouseControl { get; set; } = true;

        public const string DesktopHelp =
            "DESKTOP DEBUG (no headset)\n" +
            "Right mouse: look   WASD: move (Shift fast)   Q/E: lower/raise camera\n" +
            "Hold LEFT mouse: putter down; move mouse through the ball to putt\n" +
            "T: stand at ball   R: return ball   Backspace: restart hole\n" +
            "N: next hole   1-9: jump to hole   F1: hide this panel";
    }
}
