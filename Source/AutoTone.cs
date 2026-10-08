using System;
using System.Collections.Generic;

namespace JxrToSdr {
public sealed class ToneSettings {
    public double Exposure, Saturation = 1, Shoulder = 1;
    public ToneSettings Copy() { return (ToneSettings)MemberwiseClone(); }
}
public static class AutoTone {
    // A bounded, deterministic photographic heuristic, not a reconstruction of
    // artistic intent. Work in linear luminance; do not equalize RGB channels.
    public static ToneSettings Analyze(Frame frame) {
        var samples = new List<double>();
        // Stratified 2D sampling avoids a large allocation and row-stride aliasing.
        int nx = Math.Min(512, frame.Width), ny = Math.Min(512, frame.Height);
        for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++) {
            int sx = Math.Min(frame.Width-1, (int)((x+.5)*frame.Width/nx));
            int sy = Math.Min(frame.Height-1, (int)((y+.5)*frame.Height/ny));
            int i = (sy*frame.Width+sx)*4;
            double a = Math.Max(0, Math.Min(1, Clean(frame.Pixels[i+3])));
            double lum = (.2126*Clean(frame.Pixels[i]) + .7152*Clean(frame.Pixels[i+1]) + .0722*Clean(frame.Pixels[i+2]))*a*(80.0/203.0);
            // Ignore effectively black pixels (including letterbox bars).
            if (lum > 1e-6) samples.Add(lum);
        }
        if (samples.Count == 0) return new ToneSettings();
        samples.Sort();
        double mid = Percentile(samples, .5), bright = Percentile(samples, .90), white = Percentile(samples, .99);
        // Partial adaptation keeps a dark scene darker than a bright scene.
        double target = Math.Max(.015, Math.Min(.22, Math.Sqrt(mid*.12)));
        // Generalized Reinhard: x / (1 + x^p)^(1/p). Smooth, monotonic,
        // approaches white without hard clipping. Fit the 99th percentile to
        // 94% linear white, subject to conservative exposure/curve bounds.
        // The upper decile also constrains exposure: a dark background must
        // not push a well-lit subject toward white merely to lift the median.
        double low = .65, high = 2.4;
        for (int n = 0; n < 28; n++) {
            double p = (low+high)/2;
            double ev = ExposureFor(mid, bright, target, p);
            double mapped = Map(white*Math.Pow(2, ev), p);
            if (mapped < .94) low = p; else high = p;
        }
        double shoulder = (low+high)/2;
        return new ToneSettings {
            Exposure = Math.Round(ExposureFor(mid, bright, target, shoulder), 1),
            Saturation = 1, Shoulder = shoulder
        };
    }
    static double Clean(float x) { return float.IsNaN(x)||float.IsInfinity(x) ? 0 : x; }
    static double Percentile(List<double> values, double q) {
        double index = q*(values.Count-1); int lo = (int)index;
        return values[lo]+(values[Math.Min(lo+1,values.Count-1)]-values[lo])*(index-lo);
    }
    static double ExposureFor(double mid, double bright, double target, double p) {
        double desired = target/Math.Pow(1-Math.Pow(target,p),1/p);
        double brightLimit = .45/Math.Pow(1-Math.Pow(.45,p),1/p);
        double exposure = Math.Min(desired/mid, brightLimit/bright);
        return Math.Max(-5, Math.Min(1.5, Math.Log(exposure,2)));
    }
    public static double Map(double luminance, double shoulder) {
        if (shoulder == 1) return luminance/(1+luminance);
        return luminance/Math.Pow(1+Math.Pow(luminance,shoulder),1/shoulder);
    }
}
}
