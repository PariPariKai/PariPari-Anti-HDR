# Verification notes

The initial release was built and checked locally on Windows x64. These are completed development checks, not a claim of compatibility with every HDR capture pipeline.

- Converted eight 2560×1440 HDR JXR screenshots decoded as floating-point scRGB.
- Checked original output dimensions, RGB8 encoding, and sRGB/gamma metadata.
- Compared exported PNG pixels with the full-resolution preview.
- Checked unique output names, originals left intact, and per-source output folders.
- Checked Auto on black and dark inputs, isolated extreme highlights, and invalid floating-point values.
- Checked tone-curve monotonicity and bounded automatic exposure.
- Checked per-image manual corrections, switching images, unviewed batch items and rapid selection changes.
- Checked zoom, panning, reset and preservation of the viewport during adjustments.
- Checked English/Russian switching, persistence, missing/invalid preferences and minimum window layouts.
- Checked export from the themed Save button and command-line Auto conversion.
- Confirmed that the visual redesign did not change the exported pixels.

The processing pipeline is Windows scRGB decoding → exposure and luminance tone mapping → neutral-axis gamut compression → sRGB encoding → PNG. Alpha is composited onto black. Auto uses a bounded photographic heuristic, not image generation or an artistic-intent classifier.
