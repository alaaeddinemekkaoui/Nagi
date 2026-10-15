// These integration cases share Explorer, desktop windows, global hotkeys and Core Audio.
// Run them sequentially so shell registration does not race native window teardown.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
