#version 330 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTexCoord;
layout (location = 3) in vec2 instancePos;
layout (location = 4) in vec3 instanceColor;
layout (location = 5) in vec2 instanceScale;

uniform mat4 view;
uniform mat4 projection;
uniform mat4 model;
uniform vec3 cameraPos;
uniform int useTexture;
uniform vec3 objectColor;

out vec3 FragPos;
out vec3 Normal;
out vec2 TexCoord;
out vec3 ObjectColor;

float GroundHeight(float x, float z) {
    return 2.0 * sin(x / 5.0) * cos(z / 5.0);
}

void main() {
    vec3 pos = aPos;
    vec3 normal = aNormal;

    if (useTexture == 1) {
        vec3 toCamera = normalize(cameraPos - vec3(instancePos.x, GroundHeight(instancePos.x, instancePos.y), instancePos.y));
        vec3 right = normalize(cross(vec3(0.0, 1.0, 0.0), toCamera));
        vec3 up = normalize(cross(toCamera, right));
        mat3 billboard = mat3(right, up, toCamera);
        pos = billboard * (vec3(pos.x * instanceScale.x, pos.y * 2.0, pos.z * instanceScale.y));
        normal = billboard * normal;
        mat4 instanceModel = mat4(1.0);
        instanceModel[3] = vec4(instancePos.x, GroundHeight(instancePos.x, instancePos.y), instancePos.y, 1.0);
        gl_Position = projection * view * instanceModel * vec4(pos, 1.0);
        FragPos = vec3(instanceModel * vec4(pos, 1.0));
        Normal = mat3(transpose(inverse(instanceModel))) * normal;
        ObjectColor = instanceColor;
        TexCoord = aTexCoord;
    } else {
        gl_Position = projection * view * model * vec4(pos, 1.0);
        FragPos = vec3(model * vec4(pos, 1.0));
        Normal = mat3(transpose(inverse(model))) * normal;
        ObjectColor = objectColor;
        TexCoord = vec2(0.0);
    }
}