global using System;
global using System.IO;
global using System.Collections.Generic;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Diagnostics;
global using System.Numerics;
global using System.Text.Json;
global using System.Text.Json.Serialization;

global using TinyEmbree;
global using SimpleImageIO;

global using SeeSharp.Geometry;
global using SeeSharp.Sampling;
global using SeeSharp.Common;
global using SeeSharp.Cameras;
global using SeeSharp.Images;

global using SeeSharp.Shading;
global using SeeSharp.Shading.Background;
global using SeeSharp.Shading.Materials;
global using SeeSharp.Shading.Emitters;
global using static SeeSharp.Shading.ShadingSpace;

global using SeeSharp.Integrators;
global using SeeSharp.Integrators.Common;
global using SeeSharp.Integrators.Util;
global using SeeSharp.Integrators.Bidir;