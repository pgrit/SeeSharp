#include "api/api.h"
#include "geometry/scene.h"
#include "geometry/transform.h"
#include "cameras/camera.h"
#include "image/image.h"
#include "shading/shading.h"

#include <vector>
#include <memory>
#include <iostream>
#include <unordered_map>

extern std::vector<std::unique_ptr<ground::Transform>> globalTransforms;
extern std::vector<std::unique_ptr<ground::Camera>> globalCameras;
extern std::vector<std::unique_ptr<ground::Image>> globalImages;
extern std::unique_ptr<ground::Scene> globalScene;
extern std::vector<std::unique_ptr<ground::Material>> globalMaterials;
extern std::unordered_map<int, int> globalMeshToMaterial;
extern std::vector<int> globalEmitterRegistry;

inline void _ApiCheck(bool cond, const char* file, int line, const char* func) {
    if (!cond) {
        std::cerr << "Condition not met in " << func << "(): "
                  << file << ", line " << line << std::endl;
        abort();
    }
}
#define ApiCheck(cond) _ApiCheck(cond, __FILE__, __LINE__, __func__)

inline void _SanityCheck(bool cond, const char* file, int line, const char* func) {
#ifdef SANITY_CHECKS
    if (!cond) {
        std::cerr << "Condition not met in " << func << "(): "
                  << file << ", line " << line << std::endl;
        abort();
    }
#endif
}
#define SanityCheck(cond) _SanityCheck(cond, __FILE__, __LINE__, __func__)
