#include "image/image.h"

#define TINYEXR_IMPLEMENTATION
#include "image/tinyexr.h"

namespace ground {

Image::Image(int w, int h, int numChannels)
: width(w), height(h), numChannels(numChannels)
, data(w * h * numChannels)
{

}

int Image::PixelToIndex(int row, int col) const {
    return (row * width + col) * numChannels;
}

void Image::AddValue(float x, float y, const float* value) {
    // TODO filtering support
    int first = PixelToIndex(int(y), int(x));
    for (int i = 0; i < numChannels; ++i) {
        AtomicAddFloat(data[first + i], value[i]);
    }
}

void Image::GetValue(float x, float y, float* out) const {
    int first = PixelToIndex(int(y), int(x));
    for (int i = 0; i < numChannels; ++i) {
        out[i] = data[first + i];
    }
}

void WriteImageToFileEXR(const Image& img, const std::string& filename) {
    EXRImage image;
    InitEXRImage(&image);

    image.num_channels = img.numChannels;
    image.width = img.width;
    image.height = img.height;

    // Copy image data and convert from AoS to SoA
    // Create buffers for each channel
    std::vector<std::vector<float>> channelImages;
    for (int i = 0; i < img.numChannels; ++i) {
        channelImages.emplace_back(img.width * img.height);
    }

    // Copy the data into the buffers
    float* val = (float*) alloca(sizeof(float) * img.numChannels);
    for (int r = 0; r < img.height; ++r) for (int c = 0; c < img.width; ++c) {
        img.GetValue(float(c), float(r), val);

        for (int i = 0; i < img.numChannels; ++i)
            channelImages[i][r * img.width + c] = val[i];
    }

    // Gather an array of pointers to the channel buffers, as input to TinyEXR
    float** imagePtr = (float **) alloca(sizeof(float*) * image.num_channels);
    image.images = (unsigned char**)imagePtr;

    EXRHeader header;
    InitEXRHeader(&header);

    header.num_channels = img.numChannels;

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
        // From float to half
        header.pixel_types[i] = TINYEXR_PIXELTYPE_FLOAT;
        header.requested_pixel_types[i] = TINYEXR_PIXELTYPE_HALF;
    }

    // Save the file
    const char* errorMsg = nullptr;
    const int retCode = SaveEXRImageToFile(&image, &header, filename.c_str(), &errorMsg);
    if (retCode != TINYEXR_SUCCESS) {
        FreeEXRErrorMessage(errorMsg);
        // TODO report the error with code "retCode" and message "errorMsg"
    }
}

void WriteImageToFile(const Image& img, const std::string& filename) {
    // TODO support other file formats and decide based on filename extension
    WriteImageToFileEXR(img, filename);
}

} // namespace ground