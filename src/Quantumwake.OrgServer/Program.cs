using Quantumwake.OrgServer;

// Separated from the entry point so tests can host the same server in-process,
// several at a time - which is also why nothing in here is static.
var options = OrgServerOptions.FromArguments(args);
if (!OrgMaintenance.TryRun(args, options))
    OrgServerHost.Build(options).Run();
