namespace BS3D.Screens
{
    /// <summary>
    /// The active screen's answer to "how far out of focus is the frame behind you, and in what shape" —
    /// asked by <c>BS3DGame.FinishSceneDraw</c> at resolve time, of the <b>active</b> screen only, so
    /// nothing can write the pipeline's defocus from two places. It became an interface when the session
    /// joined the pages as an answerer (#214): a host-side type switch would have kept each screen's shape
    /// as a constant living away from the ramp it describes, which is the switch shape the screen stack
    /// was built to remove.
    /// </summary>
    internal interface IFrameBlurSource
    {
        /// <summary>How far the frame has gone out of focus, 0–1 — the amount handed to
        /// <see cref="Prazsky.Core.Render.PostProcessPipeline.Resolve"/>.</summary>
        float FrameBlur { get; }

        /// <summary>The blur's shape — the resolve's <c>defocusFocus</c>: 0 blurs the whole frame alike
        /// (a page over a stopped or finished game), 1 is precise aim's periphery-only lens, the centre
        /// held in focus. Stated by the screen whose ramp it is, next to that ramp.</summary>
        float FrameBlurFocus { get; }
    }
}
