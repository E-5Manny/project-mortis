using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

// Post-process: the scene renders into Target (at the view's scale), then one shader pass draws it to the screen with
// grain, a dirty grade, chromatic fringing, a beat-tightened vignette and a toll ripple.
// If the shader fails (ancient GPU), the scene is drawn untouched.
static class Post
{
    public static RenderTexture2D Target;
    static Shader _shader;
    static bool _ok;
    static int _res, _origin, _scale, _time, _beat, _toll, _ready, _quiet, _heart;

    const string Fs = """
        #version 330
        in vec2 fragTexCoord;
        in vec4 fragColor;
        out vec4 finalColor;
        uniform sampler2D texture0;
        uniform vec2 res;     // the game's size on screen, in pixels
        uniform vec2 origin;  // its bottom-left corner, GL coords (in full screen it sits inside the frame)
        uniform vec2 heart;   // GL coords within the game (origin bottom-left)
        uniform float time, beat, toll, ready, quiet, scale;

        float hash(vec2 p) { p = fract(p * vec2(123.34, 456.21)); p += dot(p, p + 45.32); return fract(p.x * p.y); }

        void main() {
            vec2 fc = gl_FragCoord.xy - origin;
            vec2 d = fc - heart;
            float dist = length(d);
            if (quiet < 0.5 && toll < 0.8) {                       // a ring of displacement leaving the heart
                float ring = toll * 520.0 * scale;
                float w = exp(-pow((dist - ring) / (26.0 * scale), 2.0)) * (1.0 - toll / 0.8);
                fc += normalize(d + 0.0001) * w * 7.0 * scale;
            }
            fc -= d * beat * 0.0025;                               // the room leans in on each beat
            vec2 uv = fc / res;

            float ca = quiet > 0.5 ? 0.0 : (beat * 0.9 + max(0.0, 1.0 - toll * 3.0) * 2.0) / res.x;   // fringing only on beat / toll
            vec2 dir = normalize(uv - 0.5 + 0.0001);
            vec3 col = vec3(texture(texture0, uv + dir * ca).r, texture(texture0, uv).g, texture(texture0, uv - dir * ca).b);

            float l = dot(col, vec3(0.299, 0.587, 0.114));
            col = mix(vec3(l), col, 0.82);                         // drain some colour
            col = col * vec3(1.07, 0.93, 0.82) + vec3(0.010, 0.004, 0.002);
            col = pow(max(col, 0.0), vec3(1.10));                  // crush toward the dark

            vec2 v = ((gl_FragCoord.xy - origin) / res - 0.5) * vec2(1.12, 1.0);
            float vig = smoothstep(0.80, 0.26 - beat * 0.05, length(v));
            col *= mix(0.28, 1.0, vig);
            col.r += (1.0 - vig) * (0.012 + beat * 0.035 + ready * 0.03);  // blood at the rim

            col += (hash(gl_FragCoord.xy + fract(time * 7.13) * vec2(97.0, 51.0)) - 0.5) * 0.06;
            col *= 0.975 + 0.025 * sin(time * 23.0) * sin(time * 7.3);    // tired flicker
            finalColor = vec4(col, 1.0);
        }
        """;

    public static void Init()
    {
        Resize(Ui.Zoom);
        _shader = LoadShaderFromMemory(null!, Fs);
        _ok = IsShaderValid(_shader);
        _res = GetShaderLocation(_shader, "res");
        _origin = GetShaderLocation(_shader, "origin");
        _scale = GetShaderLocation(_shader, "scale");
        _time = GetShaderLocation(_shader, "time");
        _beat = GetShaderLocation(_shader, "beat");
        _toll = GetShaderLocation(_shader, "toll");
        _ready = GetShaderLocation(_shader, "ready");
        _quiet = GetShaderLocation(_shader, "quiet");
        _heart = GetShaderLocation(_shader, "heart");
    }

    public static void Resize(float scale)
    {
        if (Target.Id != 0) UnloadRenderTexture(Target);
        Target = LoadRenderTexture((int)MathF.Round(630 * scale), (int)MathF.Round(520 * scale));
    }

    // Draw Target 1:1 at `at` (screen pixels, top-left) through the shader. heart is in layout coords.
    public static void Present(Vector2 at, float time, float beat, float sinceToll, float ready, bool quiet, Vector2 heart)
    {
        float w = Target.Texture.Width, h = Target.Texture.Height, s = Ui.Zoom, screenH = GetScreenHeight();
        var src = new Rectangle(0, 0, w, -h);  // render textures are stored upside down
        if (!_ok) { DrawTextureRec(Target.Texture, src, at, Color.White); return; }
        SetShaderValue(_shader, _res, new Vector2(w, h), ShaderUniformDataType.Vec2);
        SetShaderValue(_shader, _origin, new Vector2(at.X, screenH - at.Y - h), ShaderUniformDataType.Vec2);
        SetShaderValue(_shader, _scale, s, ShaderUniformDataType.Float);
        SetShaderValue(_shader, _heart, new Vector2(heart.X * s, h - heart.Y * s), ShaderUniformDataType.Vec2);
        SetShaderValue(_shader, _time, time, ShaderUniformDataType.Float);
        SetShaderValue(_shader, _beat, beat, ShaderUniformDataType.Float);
        SetShaderValue(_shader, _toll, sinceToll, ShaderUniformDataType.Float);
        SetShaderValue(_shader, _ready, ready, ShaderUniformDataType.Float);
        SetShaderValue(_shader, _quiet, quiet ? 1f : 0f, ShaderUniformDataType.Float);
        BeginShaderMode(_shader);
        DrawTextureRec(Target.Texture, src, at, Color.White);
        EndShaderMode();
    }
}
