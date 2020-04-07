
static constexpr float PI = 3.1415925f;

template<typename T, typename Fn>
T Quadrature(int numSteps, T initial, Fn integrand) {
    for (float u = 0.0f + FLT_EPSILON; u < 1.0f; u += 1.0f / numSteps) {
        for (float v = 0.0f + FLT_EPSILON; v < 1.0f; v += 1.0f / numSteps) {
            Vector2 primary{ u,v };
            initial = initial + integrand(primary);
        }
    }
    return initial / float(numSteps * numSteps);
}