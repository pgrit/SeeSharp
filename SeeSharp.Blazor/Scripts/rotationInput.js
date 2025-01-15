function deblurCanvas(canvas) {
    // https://developer.mozilla.org/en-US/docs/Web/API/Window/devicePixelRatio#correcting_resolution_in_a_canvas

    canvas.style.width = `${canvas.width}px`;
    canvas.style.height = `${canvas.height}px`;

    const scale = window.devicePixelRatio;
    canvas.width = Math.floor(canvas.width * scale);
    canvas.height = Math.floor(canvas.height * scale);

    const ctx = canvas.getContext("2d");
    ctx.scale(scale, scale);
}

function _rotationInput(canvas, component, initialAngle, drawGuide, computePos, computeAngle) {
    const ctx = canvas.getContext('2d');

    const halfWidth = canvas.width / 2;
    const halfHeight = canvas.height / 2;
    const handleRadius = 6;
    const radius = Math.min(halfWidth, halfHeight) - 2 * handleRadius;

    deblurCanvas(canvas);

    let curAngle = initialAngle;

    draw();

    function draw() {
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        // Guide line
        ctx.beginPath();
        drawGuide(ctx, halfWidth, halfHeight, radius);
        ctx.strokeStyle = 'gray';
        ctx.lineWidth = 2;
        ctx.stroke();

        // TODO-POLISH
        // Connect the handle to the center with a line
        // Add guidelines to visualize 45 degree steps

        // Handle
        ctx.beginPath();
        const handlePos = computePos(curAngle, halfWidth, halfHeight, radius);
        ctx.arc(handlePos.X, handlePos.Y, handleRadius, 0, Math.PI * 2);
        ctx.fillStyle = '#4a96af';
        ctx.strokeStyle = 'black';
        ctx.lineWidth = 1;
        ctx.fill();
    }

    let isDragging = false;

    function update(event) {
        let bounds = canvas.getBoundingClientRect();
        let x = event.clientX - bounds.left;
        let y = event.clientY - bounds.top;

        curAngle = computeAngle(x, y, halfWidth, halfHeight);

        draw();
    }

    canvas.addEventListener('mousedown', (event) => {
        isDragging = true;
        update(event);
    });

    canvas.addEventListener('mousemove', (event) => {
        if (!isDragging) return;
        update(event);
    });

    canvas.addEventListener('mouseup', () => {
        isDragging = false;
        component.invokeMethodAsync("OnValueChanged", curAngle);
    });
}

function initRotationInput(canvas, component, initialAngle) {
    _rotationInput(canvas, component, initialAngle, (ctx, halfWidth, halfHeight, radius) => {
        ctx.arc(halfWidth, halfHeight, radius, 0, Math.PI * 2);
    }, (curAngle, halfWidth, halfHeight, radius) => {
        return {
            X: halfWidth + Math.cos(curAngle) * radius,
            Y: halfHeight - Math.sin(curAngle) * radius
        };
    }, (x, y, halfWidth, halfHeight) => {
        let a = Math.atan2(halfHeight - y, x - halfWidth);
        if (a < 0) a += 2 * Math.PI;
        return a;
    });
}

function initHalfRotationInput(canvas, component, initialAngle, quarter) {
    _rotationInput(canvas, component, initialAngle, (ctx, halfWidth, halfHeight, radius) => {
        if (quarter)
            ctx.arc(halfWidth, halfHeight, radius, 0, -0.5 * Math.PI, true);
        else
            ctx.arc(halfWidth, halfHeight, radius, 0.5 * Math.PI, 1.5 * Math.PI, true);
    }, (curAngle, halfWidth, halfHeight, radius) => {
        return {
            X: halfWidth + Math.sin(curAngle) * radius,
            Y: halfHeight - Math.cos(curAngle) * radius
        };
    }, (x, y, halfWidth, halfHeight) => {
        let a = Math.atan2(x - halfWidth, halfHeight - y);
        if (a < 0 && a > -Math.PI * 0.5) a = 0;
        else if (a < 0) a = Math.PI;

        if (quarter)
            a = Math.min(a, 0.5 * Math.PI);

        return a;
    });
}