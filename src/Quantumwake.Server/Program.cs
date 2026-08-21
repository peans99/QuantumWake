using Quantumwake.Server;

// Entry point for the standalone server. Everything it does lives in
// ServerHost, which the overlay shell starts in-process instead - see
// ServerHost for why.
ServerHost.Build(args).Run();
