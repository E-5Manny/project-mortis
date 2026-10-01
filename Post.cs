using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

// Full-screen post-process: the scene renders into Target, then one shader pass draws it with
// grain, a dirty grade, chromatic fringing, a beat-tightened vignette and a toll ripple.
// If the shader fails (ancient GPU), the scene is drawn untouched.
static class Post
{
    public static RenderTexture2D Target;
    static Shader _shader;
    static bool _ok;
    static int _res, _time, _beat, _toll, _ready, _quiet, _heart;

    const string Fs = """
        #version 330
        in vec2 fragTexCoord;
        in vec4 fragColor;
        out vec4 finalColor;
        uniform sampler2D texture0;
        uniform vec2 res;
        uniform vec2 heart;   // GL coords (origin bottom-left)
        uniform float time, beat, toll, ready, quiet;

        float hash(vec2 p) { p = fract(p * vec2(123.34, 456.21)); p += dot(p, p + 45.32); return fract(p.x * p.y); }

        void main() {
            vec2 fc = gl_FragCoord.xy;
            vec2 d = fc - heart;
            float dist = length(d);
            if (quiet < 0.5 && toll < 0.8) {                       // a ring of displacement leaving the heart
                float ring = toll * 520.0;
                float w = exp(-pow((dist - ring) / 26.0, 2.0)) * (1.0 - toll / 0.8);
                fc += normalize(d + 0.0001) * w * 7.0;
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

            vec2 v = (gl_FragCoord.xy / res - 0.5) * vec2(1.12, 1.0);
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
        Target = LoadRenderTexture(630, 520);
        _shader = LoadShaderFromMemory(null!, Fs);
        _ok = IsShaderValid(_shader);
        _res = GetShaderLocation(_shader, "res");
        _time = GetShaderLocation(_shader, "time");
        _beat = GetShaderLocation(_shader, "beat");
        _toll = GetShaderLocation(_shader, "toll");
        _ready = GetShaderLocation(_shader, "ready");
        _quiet = GetShaderLocation(_shader, "quiet");
        _heart = GetShaderLocation(_shader, "heart");
    }

    // Draw Target to the screen through the shader. heart is in screen coords (top-left origin).
    public static void Present(float time, float beat, float sinceToll, float ready, bool quiet, Vector2 heart)
    {
        var src = new Rectangle(0, 0, 630, -520);  // render textures are stored upside down
        if (!_ok) { DrawTextureRec(Target.Texture, src, Vector2.Zero, Color.White); return; }
        float w = GetRenderWidth(), h = GetRenderHeight();
        SetShaderValue(_shader, _res, new Vector2(w, h), ShaderUniformDataType.Vec2);
        SetShaderValue(_shader, _heart, new Vector2(heart.X * w / 630, h - heart.Y * h / 520), ShaderUniformDataType.Vec2);
        SetShaderValue(_shader, _time, time, ShaderUniformDataType.Float);
        SetShaderValue(_shader, _beat, beat, ShaderUniformDataType.Float);
        SetShaderValue(_shader, _toll, sinceToll, ShaderUniformDataType.Float);
        SetShaderValue(_shader, _ready, ready, ShaderUniformDataType.Float);
        SetShaderValue(_shader, _quiet, quiet ? 1f : 0f, ShaderUniformDataType.Float);
        BeginShaderMode(_shader);
        DrawTextureRec(Target.Texture, src, Vector2.Zero, Color.White);
        EndShaderMode();
    }
}
