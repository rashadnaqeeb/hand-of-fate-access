using Xunit;

// SpeechPipeline and SpeechEngine are static singletons; running tests that
// mutate their shared state in parallel would be flaky.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
