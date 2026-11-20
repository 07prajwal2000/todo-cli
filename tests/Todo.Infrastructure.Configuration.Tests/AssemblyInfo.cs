// Several tests mutate process-wide environment variables (TODO_CONFIG_DIR, XDG_CONFIG_HOME);
// serial execution keeps them from leaking into unrelated assertions.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
