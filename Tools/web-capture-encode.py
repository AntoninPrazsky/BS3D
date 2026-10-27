"""Encodes the Game's capture frames (#946) for the website: a looping clip as WebM (VP9) and MP4 (H.264) plus a poster JPEG, or, with
--still, one frame as the gallery's pair of JPEGs.

Run by Tools/web-capture.ps1, with a Python that has PyAV (its bundled FFmpeg: no ffmpeg is installed) and Pillow:

    python -I Tools/web-capture-encode.py --frames <folder of frame-NNNNNN.png> --fps 60 --out <path without extension>

The frames are decoded once and fed to both encoders. Both videos are yuv420p, converted from RGB and tagged as BT.709 (what a
browser assumes of HD video; the default conversion is BT.601, which shifts every colour), have no audio and no loop flag of their
own (the page's <video loop> loops them), and the MP4 carries +faststart so it plays while it downloads.
"""
import argparse
import glob
import os
import sys

import av
import numpy as np
from av.video.reformatter import Colorspace
from PIL import Image

# CRF is the quality dial: lower is better and bigger. These hold the sky's gradients and the film grain of the game's own picture
# without banding; the sizes a clip comes to are in docs/website.md
VP9 = ("libvpx-vp9", {"crf": "31", "b": "0", "row-mt": "1", "cpu-used": "2", "deadline": "good", "lag-in-frames": "25"}, {})
H264 = ("libx264", {"crf": "21", "preset": "slow", "profile": "high"}, {"movflags": "+faststart"})


def frame_files(folder):
    files = sorted(glob.glob(os.path.join(folder, "frame-*.png")))
    if not files:
        sys.exit("no frame-*.png in " + folder)
    return files


def open_output(path, fps, width, height, spec):
    codec, options, container_options = spec
    container = av.open(path, "w", options=container_options)
    stream = container.add_stream(codec, rate=fps)
    stream.width = width
    stream.height = height
    stream.pix_fmt = "yuv420p"
    context = stream.codec_context
    context.options = options
    # A key every two seconds: a clip is short and a loop's restart is a seek
    context.gop_size = max(1, round(fps * 2))
    # BT.709 stated as well as used: primaries, transfer, matrix, and the limited range a browser expects of HD video
    context.color_primaries = 1
    context.color_trc = 1
    context.colorspace = 1
    context.color_range = 1
    return container, stream


def encode_video(files, fps, base, video_width):
    with Image.open(files[0]) as first:
        # 4:2:0 pairs its pixels: an odd edge loses one
        source_width, source_height = first.width // 2 * 2, first.height // 2 * 2

    # A narrower video is the frames scaled (by FFmpeg's own scaler, with the colour conversion), keeping the aspect
    width = video_width // 2 * 2 if 0 < video_width < source_width else source_width
    height = round(source_height * width / source_width) // 2 * 2

    outputs = [open_output(base + ".webm", fps, width, height, VP9), open_output(base + ".mp4", fps, width, height, H264)]

    for index, path in enumerate(files):
        with Image.open(path) as image:
            pixels = np.ascontiguousarray(np.asarray(image.convert("RGB"))[:source_height, :source_width])
        source = av.VideoFrame.from_ndarray(pixels, format="rgb24")
        # sRGB-coded RGB in; BT.709 YUV out
        frame = source.reformat(width=width, height=height, format="yuv420p", dst_colorspace=Colorspace.ITU709)
        frame.pts = index
        for container, stream in outputs:
            for packet in stream.encode(frame):
                container.mux(packet)

    for container, stream in outputs:
        for packet in stream.encode():
            container.mux(packet)
        container.close()

    for extension in (".webm", ".mp4"):
        print("%s: %.0f KB" % (base + extension, os.path.getsize(base + extension) / 1024))


def poster(files, number, base):
    number = min(max(number, 1), len(files))
    path = base + "-poster.jpg"
    with Image.open(files[number - 1]) as image:
        image.convert("RGB").save(path, "JPEG", quality=86, optimize=True, progressive=True)
    print("%s: %.0f KB (frame %d)" % (path, os.path.getsize(path) / 1024, number))


def still(files, base):
    with Image.open(files[0]) as image:
        image = image.convert("RGB")
        for width in (1600, 800):
            path = "%s-%d.jpg" % (base, width)
            image.resize((width, round(width * image.height / image.width)), Image.LANCZOS).save(
                path, "JPEG", quality=84, optimize=True, progressive=True)
            print("%s: %.0f KB" % (path, os.path.getsize(path) / 1024))


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--frames", required=True, help="the folder of frame-NNNNNN.png")
    parser.add_argument("--fps", required=True, type=float)
    parser.add_argument("--out", required=True, help="the files' path without an extension")
    parser.add_argument("--video-width", type=int, default=0, help="scale the videos down to this width (0: the frames' own)")
    parser.add_argument("--poster-frame", type=int, default=1, help="the 1-based frame the poster is made of")
    parser.add_argument("--still", action="store_true", help="write the first frame as <out>-1600.jpg and <out>-800.jpg and no video")
    arguments = parser.parse_args()

    files = frame_files(arguments.frames)
    if arguments.still:
        still(files, arguments.out)
        return

    encode_video(files, round(arguments.fps), arguments.out, arguments.video_width)
    poster(files, arguments.poster_frame, arguments.out)


if __name__ == "__main__":
    main()
