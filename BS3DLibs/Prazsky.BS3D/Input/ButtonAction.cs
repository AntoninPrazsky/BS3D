using Microsoft.Xna.Framework.Input;
using System;

namespace Prazsky.BS3D.Input
{
    public class ButtonAction
    {
        public Keys Key { get; }
        public Buttons Button { get; }
        public Action Method { get; }
        public string Description { get; }

        public ButtonAction(Keys key, Action method)
        {
            Key = key;
            Button = Buttons.None;
            Method = method;
            Description = string.Empty;
        }

        public ButtonAction(Keys key, Action method, string description)
        {
            Key = key;
            Button = Buttons.None;
            Method = method;
            Description = description;
        }

        public ButtonAction(Keys key, Buttons button, Action method, string description)
        {
            Key = key;
            Button = button;
            Method = method;
            Description = description;
        }
    }
}
