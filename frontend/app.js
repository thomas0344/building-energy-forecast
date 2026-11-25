const el = id => document.getElementById(id);
const fmt = n => n.toLocaleString('en-GB', {maximumFractionDigits: 2});
const date = row => new Date(row.timestamp);
const time = row => row.timestamp.slice(11,16);
const dayName = row => date(row).toLocaleDateString('en-GB',{weekday:'short'});
const sum = rows => rows.reduce((n,r) => n+r.predicted,0);
let forecast, hours = 168, selected = 0;

function stat(label,value,note,cls='') {
  return `<div class="${cls}"><span>${label}</span><b>${value}</b><small>${note}</small></div>`;
}
function table(headers,rows) {
  return '<table><thead><tr>'+headers.map(h=>`<th>${h}</th>`).join('')+'</tr></thead><tbody>'+
    rows.map(row=>'<tr>'+row.map(v=>`<td>${v}</td>`).join('')+'</tr>').join('')+'</tbody></table>';
}
function chart(rows,keys) {
  const max = Math.ceil(Math.max(...rows.flatMap(r=>keys.map(k=>r[k])))) || 1;
  const x = i => 38+390*i/(rows.length-1);
  const y = v => 153-137*v/max;
  const labels = [0,.5,1].map(f=>`<text x="1" y="${y(f*max)+4}">${fmt(f*max)}</text><path d="M36 ${y(f*max)}h395" stroke="#dbe3ef"/>`);
  const lines = keys.map((key,k)=>`<polyline fill="none" stroke="${k?'#df880b':'#095fea'}" stroke-width="2.5" points="${rows.map((r,i)=>x(i)+','+y(r[key])).join(' ')}"/>`);
  const ticks = [0,.25,.5,.75,1].map(f=>{
    const i=Math.round(f*(rows.length-1));
    return `<text text-anchor="middle" x="${x(i)}" y="176">${rows.length===24?time(rows[i]):rows[i].timestamp.slice(5,10)}</text>`;
  });
  return '<svg role="img" aria-label="Hourly energy in kWh" viewBox="0 0 455 185" font-size="11" fill="#53647c">'+[...labels,...lines,...ticks].join('')+'</svg>';
}
function render(){
  const rows=forecast.slice(0,hours), days=[];
  for(let i=0;i<rows.length;i+=24) days.push(rows.slice(i,i+24));
  const peak=rows.reduce((a,b)=>a.predicted>=b.predicted?a:b);
  const jumps=rows.slice(1).map((r,i)=>({row:r,delta:r.predicted-rows[i].predicted}));
  const jump=jumps.reduce((a,b)=>a.delta>=b.delta?a:b);
  el('summary').innerHTML=stat(hours===24?'Daily forecast':'Weekly forecast',fmt(sum(rows))+' kWh','Total forecast consumption')+
    stat('Peak hour',fmt(peak.predicted)+' kWh',dayName(peak)+' '+time(peak))+
    stat('Largest increase','+'+fmt(jump.delta)+' kWh',dayName(jump.row)+' '+time(jump.row)+' vs. previous hour','increase');
  const highest=Math.max(...days.map(sum));
  el('bars').innerHTML=days.map((d,i)=>`<button class="bar ${i===selected?'active':''}" aria-pressed="${i===selected}" title="${fmt(sum(d))} kWh"><small>${fmt(sum(d))}</small><i style="height:${150*sum(d)/highest}px"></i>${dayName(d[0])}</button>`).join('');
  el('bars').querySelectorAll('button').forEach((b,i)=>b.onclick=()=>{selected=i;render()});
  const d=days[selected];
  el('day').textContent=date(d[0]).toLocaleDateString('en-GB',{weekday:'long',day:'numeric',month:'long'})+(d[0].holiday?' · Holiday': '')+' · kWh';
  el('line').innerHTML=chart(d,['predicted']);
  el('hourtable').innerHTML=table(['Hour',...d[0].loads.map(l=>l.group),'Total kWh'],d.map(r=>[time(r),...r.loads.map(l=>fmt(l.kwh)),fmt(r.predicted)]));
  for(const [id,n] of [['one',24],['week',168]]){
    el(id).classList.toggle('active',hours===n);
    el(id).setAttribute('aria-pressed',hours===n);
    el(id).onclick=()=>{hours=n;selected=0;render()};
  }
}
async function load(){
  const data=await Promise.all(['forecast?hours=168','metrics','history','equipment'].map(async path=>{
    const res=await apiFetch('/api/energy/'+path);
    if(res.status===401) location.replace('/login.html');
    if(!res.ok) throw Error(res.status);
    return res.json();
  }));
  const [rows,m,history,inventory]=data;
  forecast=rows;
  render();
  el('metrics').innerHTML=stat('MAE',fmt(m.mae)+' kWh','Average absolute error')+stat('RMSE',fmt(m.rmse)+' kWh','Weights large errors more')+stat('MAPE',fmt(m.mape)+'%','Average percentage error')+stat('R²',m.r2.toFixed(3),'Explained variation');
  el('history').innerHTML=chart(history.slice(-168),['predicted','actual']);
  el('baseline').textContent='Same hour last week averaged '+fmt(m.baselineMae)+' kWh absolute error across the test set.';
  el('inventory').innerHTML=table(['Equipment','Quantity','Watts each','Schedule'],inventory.map(e=>[e.name,e.count,e.watts,e.schedule]));
  el('status').hidden=true;
}
function nav(){document.querySelectorAll('nav a').forEach(a=>a.classList.toggle('active',a.hash===(location.hash||'#forecast')));}
onhashchange=nav;nav();
load().catch(()=>el('status').textContent='Could not load the forecast. Restart the API and refresh this page.');
