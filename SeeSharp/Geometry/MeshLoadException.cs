using System;

namespace SeeSharp.Geometry {
    public class MeshLoadException : Exception {
        public string Path { get; }
        public MeshLoadException(string message, string path) : base(message) {
            Path = path;
        }
        public MeshLoadException(string message, string path, Exception inner) : base(message, inner) {
            Path = path;
        }
    }
}