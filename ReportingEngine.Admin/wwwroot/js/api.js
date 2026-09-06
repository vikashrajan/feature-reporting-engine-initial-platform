window.api = {
  async get(url) {
    const res = await fetch(url);
    if (!res.ok) throw new Error(await readError(res));
    return res.status === 204 ? null : res.json();
  },
  async send(url, method, body) {
    const res = await fetch(url, {
      method,
      headers: { "Content-Type": "application/json" },
      body: body ? JSON.stringify(body) : undefined
    });
    if (!res.ok) throw new Error(await readError(res));
    if (res.status === 204 || res.status === 202) return null;
    const text = await res.text();
    return text ? JSON.parse(text) : null;
  }
};

async function readError(res) {
  const text = await res.text();
  try {
    const problem = JSON.parse(text);
    if (problem.errors) {
      const messages = [];
      for (const [field, errors] of Object.entries(problem.errors)) {
        const cleanField = field.replace(/^\$\./, '');
        messages.push(`${cleanField}: ${Array.isArray(errors) ? errors.join(', ') : errors}`);
      }
      if (messages.length) return messages.join(' | ');
    }
    return problem.detail || problem.title || text;
  } catch {
    return text;
  }
}

window.showMsg = function (el, text, ok) {
  el.className = "msg " + (ok ? "ok" : "error");
  el.textContent = text;
};
