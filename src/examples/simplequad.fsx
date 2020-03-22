#load "api.fsx"

// Build a basic scene

Ground.InitScene()

let vertices = [|
    0.0f; 0.0f; 0.0f;
    1.0f; 0.0f; 0.0f;
    1.0f; 1.0f; 0.0f;
    0.0f; 1.0f; 0.0f;
|]

let indices = [|
    0; 1; 2; 3
|]

let meshId = Ground.AddTriangleMesh(vertices, 4, indices, 4)

Ground.FinalizeScene()

// Render the quad with an orthographic camera

let imageWidth = 512
let imageHeight = 512

let topLeft  = [| -1.0f;  2.0f; 5.0f |]
let diag = [|  3.0f; 3.0f; 0.0f |]

let primaryRays = [
    for x in 0 .. imageWidth - 1 do
        for y in 0 .. imageHeight - 1 do
            yield (
                [|
                    topLeft.[0] + float32(x) / float32(imageWidth) * diag.[0];
                    topLeft.[1] + float32(y) / float32(imageHeight) * diag.[1];
                    5.0f
                |],
                [| 0.0f; 0.0f; -1.0f |]
            )
]

let hits = [
    for pos,dir in primaryRays do
        yield Ground.TraceSingle(pos, dir)
]

