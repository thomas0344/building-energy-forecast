// The browser talks only to ASP.NET Core. Model and database calls stay in C#.
const apiOrigin = 'http://localhost:5080';
function apiFetch(path, options = {}) {
  return fetch(new URL(path, apiOrigin), {...options, credentials:'include', cache:'no-store'});
}

async function updateHealth() {
  const status = document.querySelector('#health');
  try {
    const response = await apiFetch('/api/health');
    const health = await response.json();
    status.textContent = health.status === 'healthy' ? 'System healthy' : 'System degraded';
    status.title = 'API online · Model '+(health.trainedModel?'online':'unavailable')+
      ' · Database '+(health.database?'online':'unavailable');
  } catch {
    status.textContent = 'API unreachable';
  } finally {
    setTimeout(updateHealth, 15000);
  }
}
updateHealth();

const downloadMeter = document.querySelector('#download-meter');
if (downloadMeter) downloadMeter.onclick = async event => {
  event.preventDefault();
  try {
    const response = await apiFetch('/api/energy/dataset');
    if (response.status === 401) return location.replace('/login.html');
    if (!response.ok) throw Error();
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement('a');
    link.href = url;
    link.download = 'meter.csv';
    link.click();
    setTimeout(()=>URL.revokeObjectURL(url),1000);
  } catch { downloadMeter.textContent = 'Download failed · Retry CSV'; }
};
