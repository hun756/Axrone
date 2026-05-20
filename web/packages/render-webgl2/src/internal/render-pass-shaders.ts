export const FULLSCREEN_VERTEX_SHADER_SOURCE = `#version 300 es
precision highp float;

out vec2 vUv;

void main() {
    vec2 positions[3] = vec2[](vec2(-1.0, -1.0), vec2(3.0, -1.0), vec2(-1.0, 3.0));
    vec2 position = positions[gl_VertexID];
    vUv = position * 0.5 + 0.5;
    gl_Position = vec4(position, 0.0, 1.0);
}
`;

export const TONEMAP_FRAGMENT_SHADER_SOURCE = `#version 300 es
precision highp float;

uniform sampler2D uSource;
uniform int uMode;
uniform float uExposureScale;
uniform float uGamma;
uniform float uContrast;
uniform float uSaturation;
uniform float uShoulderStrength;
uniform float uToeStrength;
uniform int uColorSpace;

in vec2 vUv;
out vec4 outColor;

vec3 applyReinhard(vec3 color) {
    return color / (1.0 + color);
}

vec3 applyAces(vec3 color) {
    return clamp(
        (color * (2.51 * color + 0.03)) / (color * (2.43 * color + 0.59) + 0.14),
        0.0,
        1.0
    );
}

vec3 rrtAndOdtFit(vec3 value) {
    vec3 a = value * (value + 0.0245786) - 0.000090537;
    vec3 b = value * (0.983729 * value + 0.4329510) + 0.238081;
    return a / b;
}

vec3 applyAcesFitted(vec3 color) {
    const mat3 inputMatrix = mat3(
        0.59719, 0.07600, 0.02840,
        0.35458, 0.90834, 0.13383,
        0.04823, 0.01566, 0.83777
    );
    const mat3 outputMatrix = mat3(
        1.60475, -0.10208, -0.00327,
        -0.53108, 1.10813, -0.07276,
        -0.07367, -0.00605, 1.07602
    );

    color = inputMatrix * color;
    color = rrtAndOdtFit(color);
    return clamp(outputMatrix * color, 0.0, 1.0);
}

vec3 applyFilmic(vec3 color) {
    color = max(vec3(0.0), color - vec3(uToeStrength * 0.02));
    return clamp(
        (color * (6.2 * color + 0.5 + uShoulderStrength)) /
            (color * (6.2 * color + 1.7 + uShoulderStrength * 2.0) + 0.06 + uToeStrength * 0.02),
        0.0,
        1.0
    );
}

vec3 agxDefaultContrastApprox(vec3 value) {
    vec3 value2 = value * value;
    vec3 value4 = value2 * value2;
    return 15.5 * value4 * value2 -
        40.14 * value4 * value +
        31.96 * value4 -
        6.868 * value2 * value +
        0.4298 * value2 +
        0.1191 * value -
        0.00232;
}

vec3 applyAgx(vec3 color) {
    const mat3 inputMatrix = mat3(
        0.842479062253094, 0.0423282422610123, 0.0423756549057051,
        0.0784335999999992, 0.878468636469772, 0.0784336,
        0.0792237451477643, 0.0791661274605434, 0.879142973793104
    );
    const mat3 outputMatrix = mat3(
        1.19687900512017, -0.0528968517574562, -0.0529716355144438,
        -0.0980208811401368, 1.15190312990417, -0.0980434501171241,
        -0.0990297440797205, -0.0989611768448433, 1.15107367264116
    );

    color = inputMatrix * max(color, vec3(0.0));
    color = log2(max(color, vec3(1e-6)));
    color = clamp((color + 12.47393) / 16.5, 0.0, 1.0);
    color = agxDefaultContrastApprox(color);
    return clamp(outputMatrix * color, 0.0, 1.0);
}

vec3 applyNeutral(vec3 color) {
    const float startCompression = 0.76;
    const float desaturation = 0.15;

    float minimumChannel = min(color.r, min(color.g, color.b));
    float offset = minimumChannel < 0.08
        ? minimumChannel - 6.25 * minimumChannel * minimumChannel
        : 0.04;
    color -= offset;

    float peak = max(color.r, max(color.g, color.b));
    if (peak < startCompression) {
        return color;
    }

    float distance = 1.0 - startCompression;
    float compressedPeak = 1.0 - distance * distance / (peak + distance - startCompression);
    color *= compressedPeak / peak;

    float g = 1.0 - 1.0 / (desaturation * (peak - compressedPeak) + 1.0);
    return mix(color, vec3(compressedPeak), g);
}

vec3 applyTonemap(vec3 color) {
    if (uMode == 1) {
        return applyReinhard(color);
    }
    if (uMode == 2) {
        return applyAces(color);
    }
    if (uMode == 3) {
        return applyAcesFitted(color);
    }
    if (uMode == 4) {
        return applyFilmic(color);
    }
    if (uMode == 5) {
        return applyAgx(color);
    }
    if (uMode == 6) {
        return applyNeutral(color);
    }
    return max(color, vec3(0.0));
}

void main() {
    vec4 sampled = texture(uSource, vUv);
    vec3 color = sampled.rgb * uExposureScale;
    color = applyTonemap(color);

    float luminance = dot(color, vec3(0.2126, 0.7152, 0.0722));
    color = mix(vec3(luminance), color, uSaturation);
    color = (color - 0.5) * uContrast + 0.5;
    color = clamp(color, 0.0, 1.0);

    if (uColorSpace == 0 || uColorSpace == 1 || uColorSpace == 2) {
        color = pow(color, vec3(1.0 / max(uGamma, 0.0001)));
    }

    outColor = vec4(color, sampled.a);
}
`;

export const POST_PROCESS_FRAGMENT_SHADER_SOURCE = `#version 300 es
precision highp float;

uniform sampler2D uSource;
uniform int uEffectMode;
uniform vec2 uTexelSize;
uniform vec4 uPrimary;
uniform vec3 uColor;
uniform vec3 uLift;
uniform vec3 uGammaVec;
uniform vec3 uGain;
uniform float uFrameSeed;

in vec2 vUv;
out vec4 outColor;

float luma(vec3 color) {
    return dot(color, vec3(0.299, 0.587, 0.114));
}

float hash12(vec2 value) {
    vec3 value3 = fract(vec3(value.xyx) * 0.1031);
    value3 += dot(value3, value3.yzx + 33.33);
    return fract((value3.x + value3.y) * value3.z);
}

vec3 sampleSource(vec2 uv) {
    return texture(uSource, clamp(uv, uTexelSize * 0.5, vec2(1.0) - uTexelSize * 0.5)).rgb;
}

vec3 applyFxaa(vec2 uv) {
    vec3 rgbM = sampleSource(uv);
    vec3 rgbNW = sampleSource(uv + vec2(-1.0, -1.0) * uTexelSize);
    vec3 rgbNE = sampleSource(uv + vec2(1.0, -1.0) * uTexelSize);
    vec3 rgbSW = sampleSource(uv + vec2(-1.0, 1.0) * uTexelSize);
    vec3 rgbSE = sampleSource(uv + vec2(1.0, 1.0) * uTexelSize);

    float lumaNW = luma(rgbNW);
    float lumaNE = luma(rgbNE);
    float lumaSW = luma(rgbSW);
    float lumaSE = luma(rgbSE);
    float lumaM = luma(rgbM);
    float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
    float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));
    float edgeThreshold = max(uPrimary.z, lumaMax * uPrimary.y);

    if ((lumaMax - lumaMin) < edgeThreshold) {
        return rgbM;
    }

    vec2 direction;
    direction.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
    direction.y = (lumaNW + lumaSW) - (lumaNE + lumaSE);

    float directionReduce = max(
        (lumaNW + lumaNE + lumaSW + lumaSE) * (0.25 * max(uPrimary.x, 0.001)),
        1.0 / 128.0
    );
    float inverseDirectionAdjustment = 1.0 / (min(abs(direction.x), abs(direction.y)) + directionReduce);
    direction = clamp(direction * inverseDirectionAdjustment, vec2(-8.0), vec2(8.0)) * uTexelSize;

    vec3 rgbA = 0.5 * (
        sampleSource(uv + direction * (1.0 / 3.0 - 0.5)) +
        sampleSource(uv + direction * (2.0 / 3.0 - 0.5))
    );
    vec3 rgbB = rgbA * 0.5 + 0.25 * (
        sampleSource(uv + direction * -0.5) +
        sampleSource(uv + direction * 0.5)
    );
    float lumaB = luma(rgbB);

    return (lumaB < lumaMin || lumaB > lumaMax)
        ? rgbA
        : mix(rgbA, rgbB, clamp(uPrimary.x, 0.0, 1.0));
}

vec3 applyVignette(vec3 color, vec2 uv) {
    float intensity = clamp(uPrimary.x, 0.0, 1.0);
    float smoothness = clamp(uPrimary.y, 0.001, 1.0);
    float roundness = clamp(uPrimary.z, 0.05, 1.0);
    vec2 centered = uv * 2.0 - 1.0;
    centered.x *= mix(1.0, 1.35, 1.0 - roundness);
    float distanceToCenter = length(centered);
    float mask = smoothstep(1.0 - smoothness, 1.0, distanceToCenter);
    return mix(color, uColor, mask * intensity);
}

vec3 applyFilmGrain(vec3 color, vec2 uv) {
    float intensity = max(uPrimary.x, 0.0);
    float response = clamp(uPrimary.y, 0.0, 1.0);
    float noise = hash12(uv * vec2(1280.0, 720.0) + vec2(uFrameSeed, uFrameSeed * 1.37));
    float grain = (noise - 0.5) * intensity;
    float luminanceResponse = mix(1.0, 1.0 - luma(color), response);
    return clamp(color + vec3(grain * luminanceResponse), 0.0, 1.0);
}

vec3 applyChromaticAberration(vec2 uv) {
    vec2 offset = (uv - 0.5) * (uPrimary.x * 0.075);
    float red = sampleSource(uv + offset).r;
    float green = sampleSource(uv).g;
    float blue = sampleSource(uv - offset).b;
    return vec3(red, green, blue);
}

vec3 applyColorGrading(vec3 color) {
    float contrast = uPrimary.x;
    float saturation = uPrimary.y;
    float temperature = uPrimary.z;
    float tint = uPrimary.w;

    color *= vec3(
        1.0 + temperature * 0.05 - tint * 0.02,
        1.0,
        1.0 - temperature * 0.05 + tint * 0.02
    );
    color = max(color * uGain + (uLift - vec3(1.0)), vec3(0.0));
    color = pow(max(color, vec3(1e-4)), 1.0 / max(uGammaVec, vec3(1e-4)));

    float luminance = luma(color);
    color = mix(vec3(luminance), color, saturation);
    color = (color - 0.5) * contrast + 0.5;
    return clamp(color, 0.0, 1.0);
}

void main() {
    vec4 sampled = texture(uSource, vUv);
    vec3 color = sampled.rgb;

    if (uEffectMode == 1) {
        color = applyFxaa(vUv);
    } else if (uEffectMode == 2) {
        color = applyVignette(color, vUv);
    } else if (uEffectMode == 3) {
        color = applyFilmGrain(color, vUv);
    } else if (uEffectMode == 4) {
        color = applyChromaticAberration(vUv);
    } else if (uEffectMode == 5) {
        color = applyColorGrading(color);
    }

    outColor = vec4(clamp(color, 0.0, 1.0), sampled.a);
}
`;