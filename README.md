# zerotomvp/rolling-systemd-update GitHub Action

We created this GitHub action to be able to have zero-downtime rolling updates of our Linux-based clusters running ASP.NET Core applications.

The target application is assumed to be using `systemd` to run, as shown (here)[https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-6.0#create-the-service-file]. The service definition MUST include `WorkingDirectory` and define the `ASPNETCORE_URLS` environment variable, so that the action can test that the service came back online.

The execution flow is:
- Connect to host.
- If `fingerprints` are set, verify them, or fail.
- Transfer the built files from `source` to `/tmp/{serviceName}.{github.run_number}.{gitub.run_attempt}`.
- If the service is running, stop it.
- Delete `{WorkingDirectory}.last`.
- Move `{WorkingDirectory}` to `{WorkingDirectory}.last`.
- Move the temporary directory with the built files to `{WorkingDirectory}`.
- Copy permissions and ownership from `{WorkingDirectory}.last` to `{WorkingDirectory}`.
- Start service.
- Probe the first binding found, path `/api/health` for a `200` status code, every 1 sec for 1 min.

If probing fails until the end of the tries, then a rollback is initiated:
- If the service is running, stop it.
- Delete `{WorkingDirectory}`.
- Move `{WorkingDirectory}.last` to `{WorkingDirectory}`.
- Start service.
- Probe the first binding found, path `/api/health` for a `200` status code, every 1 sec for 1 min.

If the rollback fails to get a healthy response, the action fails.