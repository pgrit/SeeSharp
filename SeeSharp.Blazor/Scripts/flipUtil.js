function makeFlipBook(jsonArgs, onClickObj, onClickMethodName, onWheelObj, onWheelMethodName, onMouseOverObj, onMouseOverMethodName, onKeyObj, onKeyMethodName, onCropObj, onCropMethodName) {
    function _map(obj, methodName) {
        if (!obj || !methodName)
            return null;
        return (...args) => {
            obj.invokeMethodAsync(methodName, ...args);
        }
    }

    let onClick = _map(onClickObj, onClickMethodName);
    let onWheel = _map(onWheelObj, onWheelMethodName);
    let onMouseOver = _map(onMouseOverObj, onMouseOverMethodName);
    let onKey = _map(onKeyObj, onKeyMethodName);
    let onCrop = _map(onCropObj, onCropMethodName);

    window['flipbook']['MakeFlipBook'](jsonArgs, onClick, onWheel, onMouseOver, onKey, onCrop);
}
