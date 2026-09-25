namespace SeeSharp.Common;

public class LightSampling
{
    /// <summary>
    /// Provides a strategy for sampling emitters and evaluating their selection probabilities.
    /// </summary>
    public interface ILightSamplingStrategy
    {
        /// <summary>
        /// Samples an emitter using the given random value.
        /// </summary>
        (Emitter Emitter, float Pmf) Sample(Scene scene, float rng);

        /// <summary>
        /// Returns the probability of selecting the specified emitter.
        /// </summary>
        float Pmf(Scene scene, Emitter emitter);
    }

    /// <summary>
    /// Samples emitters with equal probability.
    /// </summary>
    public sealed class UniformLightSampling : ILightSamplingStrategy
    {
        /// <summary>
        /// Selects an emitter uniformly from the scene's emitters.
        /// </summary>
        public (Emitter Emitter, float Pmf) Sample(Scene scene, float rng)
        {
            int idx = Math.Clamp((int)(rng * scene.Emitters.Count), 0, scene.Emitters.Count - 1);
            return (scene.Emitters[idx], 1.0f / scene.Emitters.Count);
        }

        /// <summary>
        /// Returns the uniform probability of selecting an emitter.
        /// </summary>
        public float Pmf(Scene scene, Emitter emitter)=> 1.0f / scene.Emitters.Count;
    }

    /// <summary>
    /// Samples emitters according to their relative power.
    /// </summary>
    public sealed class PowerLightSampling : ILightSamplingStrategy
    {
        /// <summary>
        /// Selects an emitter according to the scene's emitter power distribution.
        /// </summary>
        public (Emitter Emitter, float Pmf) Sample(Scene scene, float rng)
        {
            var distribution = scene.GetEmitterPDF();
            var (idx, _) = distribution.Sample(rng);

            return (scene.Emitters[idx], distribution.Probability(idx));
        }

        /// <summary>
        /// Returns the power-based probability of selecting the specified emitter.
        /// </summary>
        public float Pmf(Scene scene, Emitter emitter)
            => scene.GetEmitterPDF().Probability(scene.GetEmitterIndex(emitter));
    }

    /// <summary>
    /// Strategy used to determine how emitters are sampled.
    /// </summary>
    private readonly ILightSamplingStrategy strategy;

    /// <summary>
    /// Initializes a light sampler with the specified emitter sampling strategy.
    /// </summary>
    public LightSampling(ILightSamplingStrategy strategy)
    {
        this.strategy = strategy;
    }

    /// <summary>
    /// Samples an emitter using the configured sampling strategy.
    /// </summary>
    public (Emitter Emitter, float Pmf) SampleEmitter(Scene scene, float rng)=> strategy.Sample(scene, rng);

    /// <summary>
    /// Returns the probability of selecting the specified emitter using the configured sampling strategy.
    /// </summary>
    public float EmitterPmf(Scene scene, Emitter emitter)=> strategy.Pmf(scene, emitter);


    /// <summary>
    /// For cases when emission is being sampled, returns either the background or an emitter according to 
    /// the specified background probability.
    /// </summary>
    public (Emitter Emitter, float Pmf) SampleEmission(Scene scene, float rng, float backgroundProbability){
        if (rng < backgroundProbability)
            return (null, backgroundProbability);

        float emitterU = (rng - backgroundProbability) / (1.0f - backgroundProbability);

        var (emitter, emitterPmf) = strategy.Sample(scene, emitterU);

        return (emitter, (1.0f - backgroundProbability) * emitterPmf);
    }

    /// <summary>
    /// For cases when emission is being sampled, returns the probability of selecting either the 
    /// background or the specified emitter.
    /// </summary>
    public float EmissionPmf(Scene scene, Emitter emitter, float backgroundProbability){
        if (emitter == null)
            return backgroundProbability;

        return (1.0f - backgroundProbability) * strategy.Pmf(scene, emitter);
    }
}