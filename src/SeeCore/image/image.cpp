#include "api/image.h"
#include "api/api.h"

#include <unordered_map>
#include <iostream>

#define TINYEXR_IMPLEMENTATION
#include "image/tinyexr.h"

extern "C" {

SEE_CORE_API void WriteImageToExr(float* data, int width, int height, int numChannels,
                                const char* filename) {
    EXRImage image;
    InitEXRImage(&image);

    image.num_channels = numChannels;
    image.width = width;
    image.height = height;

    // Copy image data and convert from AoS to SoA
    // Create buffers for each channel
    std::vector<std::vector<float>> channelImages;
    for (int i = 0; i < numChannels; ++i) {
        channelImages.emplace_back(width * height);
    }

    // Copy the data into the buffers
    float* val = (float*) alloca(sizeof(float) * numChannels);
    for (int r = 0; r < height; ++r) for (int c = 0; c < width; ++c) {
        // Copy the values for all channels to the temporary buffer
        auto start = (r * width + c) * numChannels;
        std::copy(data + start, data + start + numChannels, val);

        // Write to the correct channel buffers
        for (int i = 0; i < numChannels; ++i)
            channelImages[i][r * width + c] = val[i];
    }

    // Gather an array of pointers to the channel buffers, as input to TinyEXR
    float** imagePtr = (float **) alloca(sizeof(float*) * image.num_channels);
    image.images = (unsigned char**)imagePtr;

    EXRHeader header;
    InitEXRHeader(&header);

    header.num_channels = numChannels;

    // Set the channel names
    std::vector<EXRChannelInfo> channels(header.num_channels);
    header.channels = channels.data();

    if (image.num_channels == 1) {
        header.channels[0].name[0] = 'Y';
        header.channels[0].name[1] = '\0';
        imagePtr[0] = channelImages[0].data();
    } else if (image.num_channels == 3) {
        header.channels[0].name[0] = 'B';
        header.channels[0].name[1] = '\0';
        imagePtr[0] = channelImages[2].data();

        header.channels[1].name[0] = 'G';
        header.channels[1].name[1] = '\0';
        imagePtr[1] = channelImages[1].data();

        header.channels[2].name[0] = 'R';
        header.channels[2].name[1] = '\0';
        imagePtr[2] = channelImages[0].data();
    } else {
        // TODO support other channel configurations as well
        //      raise error for unsupported configuration
    }

    // Define pixel type of the buffer and requested output pixel type in the file
    header.pixel_types = (int*) alloca(sizeof(int) * header.num_channels);
    header.requested_pixel_types = (int*) alloca(sizeof(int) * header.num_channels);
    for (int i = 0; i < header.num_channels; i++) {
        // From float to float
        header.pixel_types[i] = TINYEXR_PIXELTYPE_FLOAT;
        header.requested_pixel_types[i] = TINYEXR_PIXELTYPE_FLOAT;
    }

    // Save the file
    const char* errorMsg = nullptr;
    const int retCode = SaveEXRImageToFile(&image, &header, filename, &errorMsg);
    if (retCode != TINYEXR_SUCCESS) {
        FreeEXRErrorMessage(errorMsg);
        // TODO report the error with code "retCode" and message "errorMsg"
    }
}

static std::unordered_map<int, EXRHeader> headers;
static std::unordered_map<int, EXRImage> images;
static int nextExr = 0;

SEE_CORE_API int CacheExrImage(int* width, int* height, const char* filename) {
    EXRVersion exrVersion;
    int ret = ParseEXRVersionFromFile(&exrVersion, filename);
    if (ret != 0) {
        std::cerr << "Error loading '" << filename << "': Invalid .exr file. " << std::endl;
        return -1;
    }

    const char* err;
    EXRHeader exrHeader;
    ret = ParseEXRHeaderFromFile(&exrHeader, &exrVersion, filename, &err);
    if (ret) {
        std::cerr << "Error loading '" << filename << "': " << err << std::endl;
        FreeEXRErrorMessage(err);
        return -1;
    }

    // Read half as float
    for (int i = 0; i < exrHeader.num_channels; i++) {
        if (exrHeader.pixel_types[i] == TINYEXR_PIXELTYPE_HALF)
            exrHeader.requested_pixel_types[i] = TINYEXR_PIXELTYPE_FLOAT;
    }

    EXRImage exrImage;
    InitEXRImage(&exrImage);

    ret = LoadEXRImageFromFile(&exrImage, &exrHeader, filename, &err);
    if (ret) {
        std::cerr << "Error loading '" << filename << "': " << err << std::endl;
        FreeEXRHeader(&exrHeader);
        FreeEXRErrorMessage(err);
        return -1;
    }

    headers[nextExr] = exrHeader;
    images[nextExr] = exrImage;

    *width = exrImage.width;
    *height = exrImage.height;

    return nextExr++;
}

SEE_CORE_API void CopyCachedImage(int id, float* out) {
    auto& header = headers[id];
    auto& img = images[id];

    // Copy image data and convert from SoA to AoS
    int idx = 0;
    for (int r = 0; r < img.height; ++r) for (int c = 0; c < img.width; ++c) {
        for (int chan = img.num_channels - 1; chan >= 0; --chan) { // BGR -> RGB
            // TODO allow arbitrary ordering of channels and grayscale images?
            auto channel = reinterpret_cast<const float*>(img.images[chan]);
            out[idx++] = channel[r * img.width + c];

            if (img.num_channels - chan == 3) {
                // HACK to allow RGBA images. The proper solution would be to heed channel names!
                break;
            }
        }
    }

    FreeEXRImage(&img);
    FreeEXRHeader(&header);
    images.erase(id);
    headers.erase(id);
}

} // extern "C"