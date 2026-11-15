"use strict";
const el = (id) => document.getElementById(id);
let draft = null;
let dirty = false;

async function api(path, body) {
  const response = await fetch(path, body ? {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body)
  } : {});
  const data = await response.json();
  if (!response.ok) throw new Error(data.error || `Request failed (${response.status})`);
  return data;
}

function status(message, error = false) {
  el("status").textContent = message;
  el("status").classList.toggle("error", error);
  el("status").dataset.state = error ? "error" : "ready";
}

function changed() {
  dirty = true;
  el("reviewed").checked = false;
  el("export").disabled = true;
  el("export-status").textContent = "Edits stay in this tab until you export. Review again after changes.";
  updateCounts();
}

function addGroup(name = "", values = [], id = null) {
  const node = document.createElement("section");
  node.className = "group";
  node.dataset.groupId = id || crypto.randomUUID();
  const nameId = `group-${node.dataset.groupId}`;
  const label = document.createElement("label");
  label.htmlFor = nameId;
  label.textContent = "Group name (optional)";
  const input = document.createElement("input");
  input.id = nameId;
  input.className = "group-name";
  input.value = name;
  const ingredients = document.createElement("textarea");
  ingredients.rows = Math.max(3, Math.min(9, values.length + 1));
  ingredients.className = "ingredient-lines";
  ingredients.setAttribute("aria-label", "Ingredient lines");
  ingredients.value = values.join("\n");
  const remove = document.createElement("button");
  remove.type = "button";
  remove.className = "secondary";
  remove.textContent = "Remove group";
  remove.addEventListener("click", () => { node.remove(); changed(); });
  node.append(label, input, ingredients, remove);
  el("groups").append(node);
}

function currentDraft() {
  const value = { ...draft, Title: el("title").value.trim(), TimeToComplete: el("time").value.trim(),
    Notes: el("notes").value, Source: el("export-source").value.trim(), ImageData: null,
    Ingredients: [], IngredientGroups: [], Steps: [] };
  for (const node of el("groups").children) {
    const name = node.querySelector(".group-name").value.trim();
    const items = node.querySelector(".ingredient-lines").value.split(/\r?\n/).map(s => s.trim()).filter(Boolean);
    if (!items.length) continue;
    const id = name ? node.dataset.groupId : null;
    if (name) value.IngredientGroups.push({ Id: id, Name: name, Order: value.IngredientGroups.length });
    for (const item of items) value.Ingredients.push({ FQIngrediantDescription: item, GroupId: id });
  }
  value.Steps = el("steps").value.split(/\n\s*\n/).map(s => s.trim()).filter(Boolean)
    .map((Instructions, Num) => ({ Num, Instructions, ImageData: null }));
  return value;
}

function updateCounts() {
  if (!draft) return;
  const value = currentDraft();
  el("counts").textContent = `${value.Ingredients.length} ingredients / ${value.Steps.length} steps`;
}

function render(result) {
  draft = result.draft;
  el("title").value = draft.Title;
  el("time").value = draft.TimeToComplete;
  el("notes").value = draft.Notes;
  el("export-source").value = draft.Source;
  el("steps").value = draft.Steps.map(s => s.Instructions).join("\n\n");
  el("groups").replaceChildren();
  const names = new Map(draft.IngredientGroups.map(g => [g.Id, g.Name]));
  const grouped = new Map();
  for (const item of draft.Ingredients) {
    const key = item.GroupId || "";
    if (!grouped.has(key)) grouped.set(key, []);
    grouped.get(key).push(item.FQIngrediantDescription);
  }
  for (const [id, values] of grouped) addGroup(names.get(id) || "", values, id || null);
  if (!grouped.size) addGroup();
  el("warnings").replaceChildren();
  for (const warning of result.warnings.length ? result.warnings : ["Check the source before exporting."]) {
    const item = document.createElement("li");
    item.textContent = warning;
    el("warnings").append(item);
  }
  el("raw").textContent = result.raw;
  el("unassigned").textContent = result.unassigned || "None identified by these rules. Still check the original.";
  el("provenance").textContent = JSON.stringify(result.evidence, null, 2);
  el("thumbnail-panel").hidden = !result.image;
  el("thumbnail").hidden = true;
  el("thumbnail").removeAttribute("src");
  if (result.image) {
    const { preview, ...evidence } = result.image;
    el("thumbnail-evidence").textContent = JSON.stringify(evidence, null, 2);
    el("thumbnail-status").textContent = preview
      ? `Downloaded and decoded: ${evidence.width} x ${evidence.height}.`
      : evidence.reason || "No thumbnail was obtained. Recipe text is unchanged.";
    if (preview) {
      el("thumbnail").src = preview;
      el("thumbnail").hidden = false;
    }
  }
  el("workspace").hidden = false;
  dirty = false;
  el("reviewed").checked = false;
  el("export").disabled = true;
  el("export-status").textContent = "Export does not add the recipe to your collection.";
  updateCounts();
}

async function build(mode) {
  if (dirty && !window.confirm("Replace your current draft and discard its unexported edits?")) return;
  const controls = ["live", "capture", "paste", "source", "sample", "caption", "fetch-image"];
  for (const id of controls) el(id).disabled = true;
  el("workspace").inert = true;
  el("fallback-hint").hidden = true;
  status(mode === "live" ? "Fetching a fresh source and building a draft. This can take about a minute..."
    : "Building from local evidence...");
  el("status").dataset.state = "loading";
  try {
    const result = await api("/api/draft", {
      mode, source: el("source").value.trim(), caption: el("caption").value,
      include_image: el("fetch-image").checked
    });
    render(result);
    status("Draft ready. Compare it with the original source and review the flags.");
  } catch (error) {
    status(error.message + (draft ? " Previous draft unchanged." : ""), true);
    if (mode === "live") {
      el("paste-panel").open = true;
      el("fallback-hint").hidden = false;
    }
  } finally {
    for (const id of controls) el(id).disabled = false;
    el("workspace").inert = false;
  }
}

el("live").addEventListener("click", () => build("live"));
el("capture").addEventListener("click", () => build("capture"));
el("paste").addEventListener("click", () => build("paste"));
el("sample").addEventListener("change", () => { el("source").value = el("sample").value; });
el("add-group").addEventListener("click", () => { addGroup(); changed(); });
el("editor").addEventListener("input", changed);
el("reviewed").addEventListener("change", () => { el("export").disabled = !el("reviewed").checked; });
el("export").addEventListener("click", async () => {
  el("export").disabled = true;
  try {
    const result = await api("/api/export", { draft: currentDraft(), reviewed: el("reviewed").checked });
    const blob = new Blob([JSON.stringify(result.draft, null, 2) + "\n"], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `nibbles-draft-${result.draft.Id}.json`;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    dirty = false;
    el("export-status").textContent = "Reviewed JSON downloaded. Nothing was written to rfiles.";
  } catch (error) {
    el("export-status").textContent = error.message;
  } finally {
    el("export").disabled = !el("reviewed").checked;
  }
});

api("/api/samples").then(({ samples }) => {
  for (const sample of samples) {
    const option = document.createElement("option");
    option.value = sample.url;
    option.textContent = `${sample.platform.toUpperCase()} - ${sample.label}`;
    el("sample").append(option);
  }
  status(`${samples.length} captured sources available. Fetch a new URL, choose a sample, or paste source text.`);
}).catch(error => status(error.message, true));
