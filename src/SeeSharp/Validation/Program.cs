using System.Collections.Generic;
using Validation;

namespace SeeSharp.Validation {
    class Program {
        static void Main(string[] args) {
            var allTests = new List<ValidationSceneFactory>() {
                //new Validate_DirectIllum(),
                // new Validate_SingleBounce(),
                new Validate_SingleBounceGlossy(),
                //new Validate_MultiLight(),
                //new Validate_GlossyLight(),
            };

            foreach (var test in allTests)
                Validator.Validate(test);
        }
    }
}
