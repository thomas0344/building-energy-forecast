async function refreshMonitor() {
  try {
    const response = await apiFetch('/api/energy/monitor', {cache:'no-store'});
    if(response.status===401) location.replace('/login.html');
    if (!response.ok) throw Error(response.status);
    const {current,actual,alerts} = await response.json();
    const stamp = t => t.slice(0,16).replace('T',' ');
    const reading = v => v == null ? 'Missing' : fmt(v)+' kWh';
    el('meter-now').textContent = stamp(current.time)+' · Meter '+reading(actual)+' · Expected '+reading(current.expected);
    el('incidents').innerHTML = alerts.length ? table(['Building time','Alert','Expected','Meter'],
      alerts.map(a=>[stamp(a.time),a.message,reading(a.expected),reading(a.actual)])) : 'No alerts in this replay yet.';
  } catch {
    el('meter-now').textContent = 'Meter monitor offline · Reconnecting…';
  } finally {
    setTimeout(refreshMonitor,5000);
  }
}
refreshMonitor();
