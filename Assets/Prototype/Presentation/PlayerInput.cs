using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonsNShops
{
    internal enum Bind { Jump, Sprint, Interact, Use, Inventory, Crate, Crouch }

    internal interface IPlayerInput
    {
        void Orient(float yaw, float pitch);
        MoveInput Sample();
        bool Pressed(Bind action);
        bool Held(Bind action);
        bool Attack { get; }
        int Slot { get; }
    }

    internal sealed class QuietInput : IPlayerInput
    {
        public void Orient(float yaw, float pitch) { }
        public MoveInput Sample() => new();
        public bool Pressed(Bind action) => false;
        public bool Held(Bind action) => false;
        public bool Attack => false;
        public int Slot => -1;
    }

    internal sealed class InputSettings
    {
        private static readonly Key[] Defaults = { Key.Space, Key.LeftShift, Key.E, Key.Q, Key.Tab, Key.R, Key.C };
        private readonly Key[] keys = new Key[Defaults.Length];
        public float Sensitivity { get; private set; } = PlayerPrefs.GetFloat("prototype.sensitivity", 1);
        public float Volume { get; private set; } = PlayerPrefs.GetFloat("prototype.volume", 0.8f);

        public InputSettings()
        {
            for (int i = 0; i < keys.Length; i++)
                keys[i] = (Key)PlayerPrefs.GetInt("prototype.bind." + i, (int)Defaults[i]);
            AudioListener.volume = Volume;
        }

        public Key Get(Bind bind) => keys[(int)bind];
        public string Display(Bind bind) => Get(bind).ToString();
        public void Set(Bind bind, Key key)
        {
            int existing = Array.IndexOf(keys, key);
            if (existing >= 0 && existing != (int)bind)
            {
                keys[existing] = keys[(int)bind];
                PlayerPrefs.SetInt("prototype.bind." + existing, (int)keys[existing]);
            }

            keys[(int)bind] = key;
            PlayerPrefs.SetInt("prototype.bind." + (int)bind, (int)key);
            PlayerPrefs.Save();
        }

        public void Reset()
        {
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = Defaults[i];
                PlayerPrefs.DeleteKey("prototype.bind." + i);
            }
            PlayerPrefs.Save();
        }

        public void Sound(float value)
        {
            Volume = AudioListener.volume = value;
            PlayerPrefs.SetFloat("prototype.volume", value);
        }

        public void Mouse(float value)
        {
            Sensitivity = value;
            PlayerPrefs.SetFloat("prototype.sensitivity", value);
        }
    }

    internal sealed class GameplayInput : IPlayerInput, IDisposable
    {
        private static readonly Key[] SlotKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0
        };

        private readonly InputSettings settings;
        private readonly InputActionAsset actions;
        private readonly InputAction move, look, attack;
        private float yaw, pitch;

        public GameplayInput(InputSettings settings)
        {
            this.settings = settings;
            actions = UnityEngine.Object.Instantiate(InputSystem.actions);
            InputActionMap player = actions.FindActionMap("Player", true);
            move = player.FindAction("Move", true);
            look = player.FindAction("Look", true);
            attack = player.FindAction("Attack", true);
            player.Enable();
        }

        public void Orient(float yaw, float pitch)
        {
            this.yaw = yaw;
            this.pitch = pitch;
        }

        public MoveInput Sample()
        {
            Vector2 motion = look.ReadValue<Vector2>() * (settings.Sensitivity * 0.12f);
            yaw = Mathf.Repeat(yaw + motion.x, 360);
            pitch = Mathf.Clamp(pitch - motion.y, -85, 85);
            return new MoveInput
            {
                axes = move.ReadValue<Vector2>(),
                yaw = yaw,
                pitch = pitch,
                sprint = Keyboard.current?[settings.Get(Bind.Sprint)].isPressed == true,
                jump = Pressed(Bind.Jump),
                block = Mouse.current?.rightButton.isPressed == true,
                crouch = Held(Bind.Crouch)
            };
        }

        public bool Pressed(Bind action) => Keyboard.current?[settings.Get(action)].wasPressedThisFrame == true;
        public bool Held(Bind action) => Keyboard.current?[settings.Get(action)].isPressed == true;
        public bool Attack => attack.WasPressedThisFrame();
        public int Slot
        {
            get
            {
                for (int i = 0; i < SlotKeys.Length; i++)
                    if (Keyboard.current?[SlotKeys[i]].wasPressedThisFrame == true) return i;
                return -1;
            }
        }

        public static bool Rebindable(Key key) => key != Key.Escape && key != Key.F &&
            key != Key.W && key != Key.A && key != Key.S && key != Key.D &&
            key != Key.UpArrow && key != Key.DownArrow && key != Key.LeftArrow && key != Key.RightArrow &&
            !Array.Exists(SlotKeys, slot => slot == key);

        public void Dispose()
        {
            actions.Disable();
            UnityEngine.Object.Destroy(actions);
        }
    }
}
