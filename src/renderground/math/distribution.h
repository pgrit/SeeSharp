#pragma once

#include <vector>
#include <algorithm>

namespace ground
{

class Distribution1D {
public:
    template<typename FwdIter>
    void Build(FwdIter first, FwdIter last) {
        // Compute the unnormalized CDF
        cdf.resize(last - first);
        int i = 0;
        float sum = 0;
        for (FwdIter it = first; it != last; ++it) {
            sum += *it;
            cdf[i++] = sum;
        }

        // Normalize
        float total = cdf.back();
        for (auto& c : cdf) {
            c /= total;
        }

        // Force the last value to one for numerical stability
        cdf.back() = 1.0f;
    }

    unsigned int TransformPrimarySample(float primarySample, float* jacobian) const {
        // TODO sanity check that primary sample is in [0,1]

        auto it = std::upper_bound(cdf.begin(), cdf.end(), primarySample);
        unsigned int idx = it - cdf.begin();

        // Clip to the last element, e.g., if primarySample is
        // exactly 1.0 (rare but possible)
        if (it == cdf.end()) idx = cdf.size() - 1;

        *jacobian = GetJacobian(idx);
        return idx;
    }

    float GetJacobian(unsigned int idx) const {
        if (idx > 0)
            return cdf[idx] - cdf[idx - 1];
        return cdf[idx];
    }

private:
    std::vector<float> cdf;
};

} // namespace ground
