namespace Experiments {
    struct Vector3 {
        public float x, y, z;

        public Vector3(float x, float y, float z) {
            this.x = x; this.y = y; this.z = z;
        }

        public Vector3(float v = 0.0f) : this(v, v, v) { }
    }

    struct Vector2 {
        public float x, y;

        public Vector2(float x, float y) {
            this.x = x; this.y = y;
        }

        public Vector2(float v = 0.0f) : this(v, v) { }
    }

    struct Ray {
        public Vector3 origin;
        public Vector3 direction;
    }

    abstract class Camera {
        public int horizontalResolution;
        public int verticalResolution;

        Camera(int horizontalResolution, int verticalResolution) {
            this.horizontalResolution = horizontalResolution;
            this.verticalResolution = verticalResolution;
        }

        public abstract Ray SampleRay(Vector2 imagePlanePoint);
    }

    class OrthographicCamera : Camera {
        public Vector3 pos;
        public Vector3 dir;

        OrthographicCamera(int horizontalResolution, int verticalResolution,
            Vector3 pos, Vector3 dir, Vector3 spanX, Vector3 spanY)
        : base(horizontalResolution, verticalResolution)
        {
            this.pos = pos;
            this.dir = dir;
            this.spanX = spanX;
            this.spanY = spanY;
        }

        Ray Camera.SampleRay(Vector2 imagePlanePoint) {
            // Compute the correct position on the virtual image plane
            imagePlanePoint.x / horizontalResolution;
            imagePlanePoint.y / verticalResolution;

            Ray ray;
            ray.origin = Vector3();
            ray.direction = dir;
            return ray;
        }

        private Vector3 spanX;
        private Vector3 spanY;
    }
}