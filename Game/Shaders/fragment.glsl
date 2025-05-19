#version 330 core
out vec4 FragColor;
in vec3 FragPos;
in vec3 Normal;
in vec2 TexCoord;
in vec3 ObjectColor;
uniform vec3 lightPos;
uniform vec3 lightColor;
uniform float ambientStrength;
uniform sampler2D texture1;
uniform int useTexture;

void main() {
    vec3 color = useTexture == 1 ? texture(texture1, TexCoord).rgb * ObjectColor : ObjectColor;
    float alpha = useTexture == 1 ? texture(texture1, TexCoord).a : 1.0;
    if (alpha < 0.1) discard;

    vec3 ambient = ambientStrength * lightColor;
    vec3 norm = normalize(Normal);
    vec3 lightDir = normalize(lightPos - FragPos);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * lightColor;
    vec3 result = (ambient + diffuse) * color;
    FragColor = vec4(result, alpha);
}