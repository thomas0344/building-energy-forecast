const accountForm = document.querySelector('form[data-action]');
if (accountForm) accountForm.onsubmit = async event => {
  event.preventDefault();
  const feedback = document.getElementById('feedback');
  const button = accountForm.querySelector('button');
  button.disabled = true;
  feedback.textContent = '';
  try {
    const response = await apiFetch('/api/auth/'+accountForm.dataset.action, {
      method:'POST', headers:{'Content-Type':'application/json'},
      body:JSON.stringify(Object.fromEntries(new FormData(accountForm)))
    });
    if (response.ok) return location.replace('/');
    feedback.textContent = response.status === 429 ? 'Too many attempts. Try again in a minute.'
      : (await response.json()).error;
  } catch { feedback.textContent = 'Could not reach the server. Please try again.'; }
  finally { button.disabled = false; }
};
const logout = document.getElementById('logout');
if (logout) logout.onclick = async () => {
  try {
    const response = await apiFetch('/api/auth/logout', {method:'POST'});
    if (response.ok || response.status === 401) return location.replace('/login.html');
  } catch {}
  logout.textContent = 'Retry sign out';
};
