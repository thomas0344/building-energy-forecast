# Host the app on a free Linux VM

Use an Oracle Cloud **Always Free eligible** VM in your home region. Check the cost estimate before creating anything. Use an eligible shape and boot disk within your free allowance. Free capacity can be unavailable. Keep a backup of SQLite because idle VMs can be reclaimed.

1. Choose a public hostname that you control and point its DNS record at the VM's public IP. A free DNS subdomain can be used. Allow inbound TCP 80 and 443 in the cloud network and host firewall. Restrict SSH to your own address. Only the web proxy needs public access.
2. On the VM, install Docker Engine with the Compose plugin and clone this repository. From the repository root, create a file named .env containing SITE_HOST=your.actual.hostname (no https:// or trailing slash).
3. Run `docker compose up -d --build`. Caddy requests a TLS certificate and serves the dashboard. The browser uses the same public origin for /api requests. Caddy forwards those requests to ASP.NET. The Python service shares the backend network namespace and listens on loopback.
4. Open https://YOUR_HOST/api/health and verify healthy, database and trainedModel. Then open the website, create a demo account and check the forecast. After a normal container restart, sign in with the same account to verify persistent storage.
5. Use `docker compose logs --tail=80` to diagnose startup or certificate errors. The energy-state volume stores the live SQLite database and session keys. Preserve it during updates. Avoid the -v option with docker compose down, which deletes volumes.

Local development still uses the three terminals described in the root README.
