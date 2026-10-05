using Microsoft.Xna.Framework.Content;
using System;
using System.IO;

namespace BS3D
{
    /// <summary>
    /// The content manager of a Potato run (#808): every asset from where it always was, except one that also exists
    /// under <c>Content/Potato</c>, which is taken from there.
    /// <para>
    /// <b>Why.</b> The Windows build carries the desktop's effects at <c>Content/Shaders</c>, and six of the Potato
    /// tier's nine are ports of desktop effects under the SAME names (<c>ShotTrail</c>, <c>LaserGrid</c>, <c>Blast</c>,
    /// <c>Wormhole</c>, <c>Fireworks</c>, <c>Confetti</c> - "The Potato path" in <c>docs/rendering.md</c>). Their owners
    /// load them by that name from a dozen places, in the libraries too, and none of them knows which tier it is in. So
    /// <c>Prazsky.Shaders</c> compiles the Potato set into a folder of its own and this looks there first: a Potato
    /// run's <c>Load&lt;Effect&gt;("Shaders/ShotTrail")</c> gets the Potato trail, and no loader was touched.
    /// </para>
    /// <para>
    /// In GamePi there is no such folder - its own effects ARE its <c>Content/Shaders</c> - and this is the stock
    /// manager with one file test an asset.
    /// </para>
    /// </summary>
    internal sealed class PotatoContent : ContentManager
    {
        /// <summary>The folder under the content root that is looked in first.</summary>
        internal const string OVERLAY = "Potato";

        internal PotatoContent(IServiceProvider services, string rootDirectory) : base(services, rootDirectory)
        {
        }

        protected override Stream OpenStream(string assetName)
        {
            //Beside the executable, as the stock manager resolves a relative root (TitleContainer): never against the
            //working directory, which a shortcut or a script may have set to anything
            string overlaid = Path.Combine(AppContext.BaseDirectory, RootDirectory, OVERLAY, assetName + ".xnb");

            return File.Exists(overlaid) ? File.OpenRead(overlaid) : base.OpenStream(assetName);
        }
    }
}
