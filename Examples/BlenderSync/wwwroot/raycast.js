window.getBlenderPixelCoords = function (imgId, clickX, clickY) {
    const img = document.getElementById(imgId);
    if (!img) return null;

    const rect = img.getBoundingClientRect();

    // Pixel inside displayed image
    const x_ui = clickX - rect.left;
    const y_ui = clickY - rect.top;

    // Displayed size
    const Wu = rect.width;
    const Hu = rect.height;

    // Blender render resolution
    const Wb = img.naturalWidth;
    const Hb = img.naturalHeight;

    return {
        x: x_ui / Wu * Wb,
        y: y_ui / Hu * Hb
    };
};