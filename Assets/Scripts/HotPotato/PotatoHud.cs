using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HotPotato
{
    public sealed class PotatoHud
    {
        readonly HotPotatoGame game;
        readonly RectTransform safe;
        readonly TMP_FontAsset font;
        readonly Sprite rounded;
        readonly Texture2D texture;
        readonly Canvas canvas;
        readonly GameObject lobby, modal, gameplay, jumpButton;
        readonly TMP_Text timer, alive, countdown, modalTitle, modalDetail, primaryLabel, muteLabel, actionLabel, instructions;
        readonly Image fuseBar, actionBar, timerCard;
        readonly Image[] sizes = new Image[5];
        readonly int[] counts = { 2, 6, 8, 10, 12 };
        readonly RectTransform stickBase, stickKnob, menuCard, resultCard;

        public PotatoHud(HotPotatoGame game, Transform parent)
        {
            this.game = game;
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "UI corners", filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = Mathf.Max(16 - x, x - 47, 0), dy = Mathf.Max(16 - y, y - 47, 0);
                    texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(16 - Mathf.Sqrt(dx * dx + dy * dy))));
                }
            texture.Apply();
            rounded = Sprite.Create(texture, new Rect(0, 0, 64, 64), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect, new Vector4(20, 20, 20, 20));
            var obj = new GameObject("Interface", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            obj.transform.SetParent(parent, false);
            canvas = obj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            var scaler = obj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800);
            scaler.matchWidthOrHeight = 0.5f;
            if (EventSystem.current == null)
            {
                var events = new GameObject("UI input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(parent, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            safe = Rect("Safe area", obj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            gameplay = Rect("Match HUD", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            var timeRoot = Anchor("Fuse", gameplay.transform, new Vector2(0.5f, 1), new Vector2(0, -62), new Vector2(150, 84));
            timerCard = Panel(timeRoot, PotatoView.Ink);
            timer = Label("--", timeRoot, 43, PotatoView.Cream);
            var track = Anchor("Fuse track", timeRoot, new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(118, 4));
            fuseBar = Fill(track, PotatoView.Coral);
            alive = Label("", Anchor("Remaining", gameplay.transform, new Vector2(0, 1), new Vector2(90, -48), new Vector2(140, 40)), 23, PotatoView.Ink);
            Button("PAUSE", gameplay.transform, new Vector2(1, 1), new Vector2(-75, -48), new Vector2(106, 40), PotatoView.Ink, PotatoView.Cream, game.TogglePause);
            countdown = Label("", Anchor("Countdown", gameplay.transform, Vector2.one * 0.5f, Vector2.zero, new Vector2(500, 160)), 90, PotatoView.Ink);
            var action = Anchor("Action cooldown", gameplay.transform, new Vector2(0.5f, 0), new Vector2(0, 47), new Vector2(140, 6));
            Panel(action, new Color(0.8f, 0.82f, 0.75f));
            actionBar = Fill(action, PotatoView.Mint);
            actionLabel = Label("LUNGE", Anchor("Action", gameplay.transform, new Vector2(0.5f, 0), new Vector2(0, 74), new Vector2(200, 28)), 15, PotatoView.Ink);
            jumpButton = Button("JUMP", gameplay.transform, new Vector2(1, 0), new Vector2(-80, 85), new Vector2(112, 70), PotatoView.Ink, PotatoView.Cream, game.RequestJump);

            stickBase = Anchor("Touch stick", safe, Vector2.zero, Vector2.zero, new Vector2(105, 105));
            Panel(stickBase, new Color(0.13f, 0.19f, 0.26f, 0.2f));
            stickKnob = Anchor("Thumb", stickBase, Vector2.one * 0.5f, Vector2.zero, new Vector2(44, 44));
            Panel(stickKnob, new Color(1, 0.98f, 0.9f, 0.8f));

            lobby = Overlay("Lobby", safe);
            menuCard = Anchor("Menu", lobby.transform, Vector2.one * 0.5f, Vector2.zero, new Vector2(500, 470));
            Panel(menuCard, PotatoView.Cream, true);
            Label("HOT POTATO", Anchor("Title", menuCard, new Vector2(0.5f, 1), new Vector2(0, -62), new Vector2(460, 76)), 48, PotatoView.Ink);
            Label("PLAYERS", Anchor("Players", menuCard, new Vector2(0.5f, 1), new Vector2(0, -126), new Vector2(300, 25)), 16, PotatoView.Ink);
            for (int i = 0; i < counts.Length; i++)
            {
                int count = counts[i];
                sizes[i] = Button(count.ToString(), menuCard, new Vector2(0.5f, 1), new Vector2((i - 2) * 73, -166), new Vector2(63, 44), PotatoView.Mint, PotatoView.Ink, () => game.SetPlayerCount(count)).GetComponent<Image>();
            }
            Button("PLAY", menuCard, new Vector2(0.5f, 1), new Vector2(0, -244), new Vector2(350, 60), PotatoView.Coral, Color.white, game.StartMatch);
            instructions = Label("", Anchor("Controls", menuCard, new Vector2(0.5f, 1), new Vector2(0, -345), new Vector2(450, 112)), 18, PotatoView.Ink);
            var mute = Button("SOUND ON", menuCard, new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(160, 30), PotatoView.Cream, PotatoView.Ink, game.ToggleMute);
            muteLabel = mute.GetComponentInChildren<TMP_Text>();

            modal = Overlay("Pause and results", safe);
            resultCard = Anchor("Results", modal.transform, Vector2.one * 0.5f, Vector2.zero, new Vector2(470, 340));
            Panel(resultCard, PotatoView.Cream, true);
            modalTitle = Label("", Anchor("Result", resultCard, new Vector2(0.5f, 1), new Vector2(0, -65), new Vector2(430, 70)), 40, PotatoView.Ink);
            modalDetail = Label("", Anchor("Details", resultCard, new Vector2(0.5f, 1), new Vector2(0, -128), new Vector2(430, 45)), 20, PotatoView.Ink);
            primaryLabel = Button("RESUME", resultCard, new Vector2(0.5f, 1), new Vector2(0, -207), new Vector2(340, 55), PotatoView.Coral, Color.white,
                () => { if (game.Paused) game.TogglePause(); else game.StartMatch(); }).GetComponentInChildren<TMP_Text>();
            Button("LOBBY", resultCard, new Vector2(0.5f, 1), new Vector2(0, -276), new Vector2(340, 44), PotatoView.Ink, PotatoView.Cream, game.BackToLobby);
        }

        public void Refresh(PotatoInput input)
        {
            Rect area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            bool inLobby = game.CurrentStage == HotPotatoGame.Stage.Lobby;
            bool finished = game.CurrentStage == HotPotatoGame.Stage.Finished;
            bool touch = Touchscreen.current != null;
            lobby.SetActive(inLobby);
            gameplay.SetActive(!inLobby && !finished);
            modal.SetActive(game.Paused || finished);
            jumpButton.SetActive(touch && game.Actors[0].Alive);
            float width = area.width / canvas.scaleFactor;
            menuCard.sizeDelta = new Vector2(Mathf.Min(500, width - 24), 470);
            resultCard.sizeDelta = new Vector2(Mathf.Min(470, width - 24), 340);
            Set(timer, game.Rules.Running ? (Mathf.Ceil(game.Rules.Fuse * 10) / 10).ToString("0.0") : "--");
            timerCard.color = game.Rules.Running && game.Rules.Fuse < 3 ? PotatoView.Coral : PotatoView.Ink;
            fuseBar.color = game.Rules.Fuse < 3 ? PotatoView.Cream : PotatoView.Coral;
            fuseBar.rectTransform.anchorMax = new Vector2(game.Rules.Running ? game.Rules.Fuse / game.Rules.RoundDuration : 0, 1);
            Set(alive, game.Rules.Remaining + " LEFT");
            Set(countdown, game.CurrentStage == HotPotatoGame.Stage.Countdown ? Mathf.Max(1, Mathf.CeilToInt(game.StageTime)).ToString() : "");
            actionBar.rectTransform.anchorMax = new Vector2(game.AimingThrow ? game.Charge : game.DashReady, 1);
            actionBar.color = game.AimingThrow ? PotatoView.Coral : PotatoView.Mint;
            Set(actionLabel, !game.Actors[0].Alive ? "SPECTATING" : game.AimingThrow ? "THROW" : "LUNGE");
            for (int i = 0; i < counts.Length; i++) sizes[i].color = game.PlayerCount == counts[i] ? PotatoView.Coral : PotatoView.Mint;
            Set(modalTitle, game.Paused ? "PAUSED" : game.Announcement);
            Set(modalDetail, game.Paused ? "" : game.Passes + " passes");
            Set(primaryLabel, game.Paused ? "RESUME" : "PLAY AGAIN");
            Set(muteLabel, game.Muted ? "SOUND OFF" : "SOUND ON");
            Set(instructions, touch ? "Drag left to move. Tap right to lunge.\nHold right, aim, release to throw.\nMiss a throw? You're still responsible."
                : Gamepad.current != null ? "Left stick move   Right stick aim\nA jump   X lunge   Hold RB to throw\nMiss a throw? You're still responsible."
                : "WASD move   Space jump   Shift lunge\nClick: lunge   Hold + release: throw\nMiss a throw? You're still responsible.");
            stickBase.gameObject.SetActive(input.StickActive && !game.Paused);
            if (input.StickActive)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, input.StickOrigin, null, out Vector2 point);
                stickBase.localPosition = point;
                stickKnob.anchoredPosition = input.Move * 33;
            }
        }

        static void Set(TMP_Text label, string value) { if (label.text != value) label.text = value; }
        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = min; rt.anchorMax = max; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }
        static RectTransform Anchor(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size) => Rect(name, parent, anchor, anchor, pos, size);
        Image Panel(RectTransform parent, Color color, bool blocks = false)
        {
            var image = parent.gameObject.AddComponent<Image>();
            image.sprite = rounded; image.type = Image.Type.Sliced; image.color = color; image.raycastTarget = blocks;
            return image;
        }
        Image Fill(Transform parent, Color color) => Panel(Rect("Fill", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), color);
        TMP_Text Label(string text, RectTransform parent, int size, Color color)
        {
            var rt = Rect("Text", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.text = text;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }
        GameObject Button(string text, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color bg, Color fg, Action action)
        {
            var rect = Anchor(text, parent, anchor, pos, size);
            var image = Panel(rect, bg, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => { action(); if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); });
            Label(text, rect, 18, fg);
            return rect.gameObject;
        }
        GameObject Overlay(string name, Transform parent)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.13f, 0.19f, 0.26f, 0.27f);
            return rect.gameObject;
        }
        public void Dispose() { UnityEngine.Object.Destroy(rounded); UnityEngine.Object.Destroy(texture); }
    }
}
