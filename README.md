# zerotomvp/rolling-systemd-update GitHub Action

We created this GitHub action to be able to have zero-downtime rolling updates of our Linux-based clusters running ASP.NET Core applications.

The target application is assumed to be using `systemd` to run, as shown [here](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-6.0#create-the-service-file). The service definition MUST include `WorkingDirectory` and define the `ASPNETCORE_URLS` environment variable, so that the action can test that the service came back online.

The execution flow is:
- Connect to host.
- If `fingerprints` are set, verify them, or fail.
- Transfer the built files from `source` to `/tmp/{serviceName}.{github.run_number}.{gitub.run_attempt}` using a tgz archive.
- If the service is running, stop it.
- Delete `{WorkingDirectory}.last`, if it exists.
- Move `{WorkingDirectory}` to `{WorkingDirectory}.last`, if it exists.
- Extract the tgz file to `{WorkingDirectory}`.
- Set permissions and ownership to `{WorkingDirectory}`.
- Start service.
- Probe the first binding found, path `/api/health` for a `200` status code, every 1 sec for 1 min.

If probing is successful, the next host is updated.

If probing fails until the end of the tries, then a rollback is initiated for all hosts that succeeded and the host that was being updated when the failure occurred:
- If `{WorkingDirectory.last}` does not exist, abort.
- If the service is running, stop it.
- Delete `{WorkingDirectory}`.
- Move `{WorkingDirectory}.last` back to `{WorkingDirectory}`.
- Start service.
- Probe the first binding found, path `/api/health` for a `200` status code, every 1 sec for 1 min.

If the rollback fails to get a healthy response, the action fails.

## Sample Service Definition

```
[Unit]
Description=App service

[Service]
WorkingDirectory=/opt/my-app
ExecStart=/usr/bin/dotnet /opt/my-app/My.App.dll
SyslogIdentifier=my-app
User=my-app
Restart=always
RestartSec=5
KillSignal=SIGINT
AmbientCapabilities=CAP_NET_BIND_SERVICE # only needed if binding to ports less than 1024
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=ASPNETCORE_URLS=http://*:5000

[Install]
WantedBy=multi-user.target
EOT
```

## Sample workflow

```
...

    - uses: zerotomvp/rolling-systemd-update@v1-alpha.17
      with:
        serviceName: my-app
        hosts: host1,host2,host3
        key: ${{ secrets.DEPLOY_OPENSSH_KEY }}
        source: my-app/publish
```