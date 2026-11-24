// MASAR+ Admin Portal — Firebase Authentication + Cloud Firestore
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.14.1/firebase-app.js";
import { firebaseConfig } from "./firebase-config.js";
import { mapsUrl, mapCoordinates } from "./hunt-maps.js";
import {
  getAuth, onAuthStateChanged, signInWithEmailAndPassword, signOut,
  sendPasswordResetEmail, updateProfile, createUserWithEmailAndPassword,
} from "https://www.gstatic.com/firebasejs/10.14.1/firebase-auth.js";
import {
  getFirestore, collection, doc, getDoc, getDocs, addDoc, updateDoc, deleteDoc, setDoc,
  serverTimestamp, writeBatch, query, where, runTransaction,
} from "https://www.gstatic.com/firebasejs/10.14.1/firebase-firestore.js";

// ================= STATE =================
let auth, db;
let admin = null;            // Existing profile object shared by admin and sponsor header
let accountRole = null;      // admin | sponsor | pending
let sponsorPartnerId = null;
let registeringSponsor = false;
let currentPage = "login";
const state = {
  rewards: [], partners: [], flags: [],
  flagFilter: "open",
  exportRows: [], exportRange: "all", analyticsRange: "all",
};
const PAGES = ["dashboard", "analytics", "rewards", "sponsors", "flags", "hunts", "settings", "sp-dashboard", "sp-products", "sp-rewards", "sp-verify", "sp-profile", "application-status"];
const SPONSOR_PAGES = ["sp-dashboard", "sp-products", "sp-rewards", "sp-verify", "sp-profile"];
const CRYSTALS_PER_KWD = 1000;

// ================= HELPERS =================
const $ = (id) => document.getElementById(id);
const esc = (v) => String(v ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
const num = (v) => (Number.isFinite(Number(v)) ? Number(v) : 0);
const fmt = (n) => num(n).toLocaleString("en-US");
const compact = (n) => new Intl.NumberFormat("en-US", { notation: "compact", maximumFractionDigits: 1 }).format(num(n));

function toDate(v) {
  if (!v) return null;
  if (typeof v.toDate === "function") return v.toDate();
  const d = new Date(v);
  return isNaN(d) ? null : d;
}
const fmtDate = (v, opts = { month: "short", day: "numeric", year: "numeric" }) => {
  const d = toDate(v);
  return d ? d.toLocaleDateString("en-US", opts) : "";
};
function timeAgo(v) {
  const d = toDate(v);
  if (!d) return "";
  const s = Math.floor((Date.now() - d) / 1000);
  if (s < 60) return "just now";
  if (s < 3600) return `${Math.floor(s / 60)}m ago`;
  if (s < 86400) return `${Math.floor(s / 3600)}h ago`;
  return `${Math.floor(s / 86400)}d ago`;
}
const initials = (s) => (s || "A").split(/[\s._@-]+/).filter(Boolean).slice(0, 2).map((p) => p[0].toUpperCase()).join("") || "A";

let toastTimer;
function showToast(msg, isError = false) {
  const t = $("toast");
  $("toast-msg").textContent = msg;
  $("toast-icon").textContent = isError ? "error" : "check_circle";
  t.classList.toggle("error", isError);
  t.classList.add("show");
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => t.classList.remove("show"), isError ? 5000 : 3000);
}

function friendlyError(e) {
  const code = e?.code || "";
  if (code.includes("permission-denied")) return `Firestore blocked access${e.collection ? ` to the "${e.collection}" collection` : ""}. Publish the latest MASAR+ rules (firebase/firestore.rules) in Firebase console → Firestore → Rules.`;
  if (code.includes("unavailable")) return "Can't reach Firestore. Check your connection.";
  return e?.message || "Something went wrong.";
}

function setBusy(btn, busy, label) {
  if (!btn) return;
  if (busy) { btn.dataset.label = btn.innerHTML; btn.disabled = true; btn.textContent = label || "Saving…"; }
  else { btn.disabled = false; if (btn.dataset.label) btn.innerHTML = btn.dataset.label; }
}

function setToggle(el, on) { el.classList.toggle("toggle-on", !!on); }
const isToggled = (el) => el.classList.contains("toggle-on");

// Load a collection, skipping the "_schema_placeholder" docs created by the setup script.
async function list(name) {
  let snap;
  try { snap = await getDocs(collection(db, name)); }
  catch (e) { e.collection = name; throw e; }
  return snap.docs
    .filter((d) => d.id !== "_schema_placeholder" && !d.data()._isPlaceholder)
    .map((d) => ({ id: d.id, ...d.data() }));
}
// Same, but returns [] on permission errors so one restricted collection doesn't break a page.
async function listSafe(name) {
  try { return await list(name); } catch (e) { console.warn(`Could not load ${name}:`, e); return []; }
}

const rewardCost = (r) => num(r.crystalCost ?? r.points);


const isRedeemed = (ur) =>
  ["issued", "fulfilled", "redeemed"].includes(
    String(ur.status || "").toLowerCase()
  ) || !!toDate(ur.redeemDate);


const emptyState = (icon, text) => `
  <div class="col-span-full text-center py-12 text-on-surface-variant">
    <span class="material-symbols-outlined" style="font-size:40px;opacity:.4">${icon}</span>
    <p class="mt-2 text-sm font-medium">${esc(text)}</p>
  </div>`;
const skeleton = (n = 3, h = "h-16") => Array.from({ length: n }, () => `<div class="skeleton ${h}"></div>`).join("");

const statusPill = (status) => {
  const s = (status || "Draft").toLowerCase();
  const cls = s === "active" ? "text-green-700 bg-green-100" : s === "ended" || s === "expired" ? "text-slate-600 bg-slate-200" : "text-amber-700 bg-amber-100";
  return `<span class="text-xs font-bold ${cls} px-2 py-1 rounded-full">${esc(status || "Draft")}</span>`;
};

// ================= NAVIGATION =================
function navigate(page, param) {
  document.querySelectorAll(".dropdown-menu").forEach((d) => d.classList.remove("open"));
  closeSidebar();
  const guest = !admin;
  if (guest) page = page === "signup" ? "signup" : "login";
  else if (accountRole === "pending") page = "application-status";
  else if (accountRole === "sponsor") page = SPONSOR_PAGES.includes(page) ? page : "sp-dashboard";
  else if (!PAGES.includes(page) || SPONSOR_PAGES.includes(page) || page === "login" || page === "application-status") page = "dashboard";

  const loggedIn = !guest;
  const sidebarVisible = loggedIn && accountRole !== "pending";
  $("sidebar").style.display = sidebarVisible ? "flex" : "none";
  $("app").classList.toggle("md:ml-64", sidebarVisible);
  document.querySelectorAll(".auth-only").forEach((el) => (el.style.visibility = loggedIn ? "visible" : "hidden"));
  document.querySelectorAll("#sidebar-nav .nav-link").forEach((el) => {
    el.style.display = (accountRole === "sponsor") === SPONSOR_PAGES.includes(el.dataset.page) ? "" : "none";
  });
  $("top-nav").style.display = accountRole === "admin" ? "" : "none";
  $("notifications-wrap").style.display = accountRole === "admin" ? "" : "none";
  $("profile-settings").style.display = accountRole === "admin" ? "" : "none";

  document.querySelectorAll(".page").forEach((p) => p.classList.remove("active"));
  $(`page-${page}`)?.classList.add("active");
  currentPage = page;
  document.querySelectorAll("#sidebar-nav .nav-link").forEach((l) => l.classList.toggle("active", l.dataset.page === page));
  document.querySelectorAll(".nav-top").forEach((l) => {
    const on = l.dataset.page === page;
    ["text-[#77c6fc]", "border-b-2", "border-[#77c6fc]", "pb-1"].forEach((c) => l.classList.toggle(c, on));
    l.classList.toggle("text-slate-400", !on);
  });
  const hash = page === "login" ? "" : `#${page}${param ? "/" + param : ""}`;
  if (location.hash !== hash) history.replaceState(null, "", hash || location.pathname);
  window.scrollTo(0, 0);
  const loader = loaders[page];
  if (loader) loader(param).catch((e) => { console.error(e); showToast(friendlyError(e), true); });
}

function toggleSidebar() {
  if (window.innerWidth >= 768) return;
  const open = $("sidebar").classList.toggle("open");
  $("sidebar-overlay").classList.toggle("show", open);
}
function closeSidebar() {
  $("sidebar").classList.remove("open");
  $("sidebar-overlay").classList.remove("show");
}

// ================= AUTH =================
function showLoginMessage(kind, msg) {
  const err = $("login-error"), info = $("login-info");
  err.classList.add("hidden"); err.classList.remove("flex");
  info.classList.add("hidden"); info.classList.remove("flex");
  if (!kind) return;
  const box = kind === "error" ? err : info;
  $(kind === "error" ? "login-error-msg" : "login-info-msg").textContent = msg;
  box.classList.remove("hidden"); box.classList.add("flex");
}

async function doLogin(e) {
  e.preventDefault();
  const email = $("login-email").value.trim();
  const password = $("login-password").value;
  if (!email || !password) return showLoginMessage("error", "Enter your email and password.");
  showLoginMessage(null);
  const btn = $("login-submit");
  setBusy(btn, true, "Verifying…");
  try {
    await signInWithEmailAndPassword(auth, email, password);
    // onAuthStateChanged takes it from here.
  } catch (err) {
    const c = err.code || "";
    const msg = c.includes("invalid-credential") || c.includes("wrong-password") || c.includes("user-not-found") || c.includes("invalid-email")
      ? "Invalid email or password. Please verify your credentials."
      : c.includes("too-many-requests") ? "Too many attempts. Please wait a moment and try again."
      : c.includes("unauthorized-domain") ? "This domain isn't authorised in Firebase Authentication. Add it under Authentication → Settings → Authorized domains."
      : err.message;
    showLoginMessage("error", msg);
  } finally {
    setBusy(btn, false);
  }
}

async function forgotPassword() {
  const email = $("login-email").value.trim();
  if (!email) return showLoginMessage("error", "Enter your email above, then click “Forgot access?” again.");
  try {
    await sendPasswordResetEmail(auth, email);
  } catch (e) { console.warn(e); }
  // Same message either way so we don't reveal which emails exist.
  showLoginMessage("info", `If ${email} has an account, a password reset link is on its way.`);
}

async function logout() {
  await signOut(auth);
  showToast("Signed out");
}

function renderProfile() {
  const name = admin.username || admin.displayName || admin.email;
  document.querySelectorAll(".admin-name").forEach((el) => (el.textContent = name));
  document.querySelectorAll(".admin-role").forEach((el) => (el.textContent = admin.role || "Administrator"));
  document.querySelectorAll(".admin-initials").forEach((el) => (el.textContent = initials(name)));
}

// ================= DASHBOARD =================
const LEGENDS = ["Tantal", "Om Elsaaf", "Bu Daryah", "Hmarat Algayla", "Sheelah"];
// A hunt counts as completed when the game has saved a walking session with an end time.
const isCompletedHunt = (x) => !!toDate(x.endTime);

// Single source of truth for activity totals, shared by Dashboard and Analytics
// so both pages always show the same numbers. Everything comes from the
// walking sessions the game saves.
function activityTotals(sessions) {
  const completed = sessions.filter(isCompletedHunt);
  const sum = (field) => completed.reduce((t, x) => t + num(x[field]), 0);
  return {
    completed,
    steps: sum("numSteps"),
    km: sum("distance"),
    crystals: sum("crystalsEarned"),
    energy: sum("energyEarned"),
  };
}

async function loadDashboard() {
  $("recent-completions").innerHTML = skeleton(3);
  const [users, flags, sessions, userRewards, rewards, partners] = await Promise.all([
    listSafe("users"), Promise.resolve([]), listSafe("walking_sessions"), listSafe("user_rewards"), listSafe("rewards"), listSafe("partners"),
  ]);
  const flagList = await computeFlags(sessions);
  state.flags = flagList;

  const totals = activityTotals(sessions);
  const { completed } = totals;
  const weekAgo = Date.now() - 7 * 864e5;
  $("stat-active").textContent = fmt(completed.length);
  $("stat-active-sub").textContent = `+${fmt(completed.filter((x) => toDate(x.endTime).getTime() > weekAgo).length)} this week`;
  $("stat-users").textContent = fmt(users.length);
  const newUsers = users.filter((u) => (toDate(u.createdAt)?.getTime() || 0) > weekAgo).length;
  $("stat-users-sub").textContent = `+${fmt(newUsers)} this week`;
  $("stat-steps").textContent = compact(totals.steps);
  $("stat-steps-sub").textContent = `${compact(totals.crystals)} crystals earned`;
  const redeemed = userRewards.filter(isRedeemed);
  const monthAgo = Date.now() - 30 * 864e5;
  $("stat-redeem").textContent = fmt(redeemed.length);
  $("stat-redeem-sub").textContent = `${fmt(redeemed.filter((r) => (toDate(r.createdAt || r.redeemDate)?.getTime() || 0) > monthAgo).length)} in last 30 days`;
  const open = flagList.filter((f) => (f.status || "open") === "open").length;
  $("stat-flags").textContent = fmt(open);
  $("qa-flags").textContent = open ? `Review ${open} Flag${open === 1 ? "" : "s"}` : "Review Flags";
  $("seed-banner").classList.toggle("hidden", rewards.length + partners.length > 0);
  renderNotifications();

  // Activity chart — hunts completed per day, last 7 days.
  const days = [];
  for (let i = 6; i >= 0; i--) {
    const d = new Date(); d.setHours(0, 0, 0, 0); d.setDate(d.getDate() - i);
    days.push({ d, count: 0 });
  }
  completed.forEach((x) => {
    const day = new Date(toDate(x.endTime)); day.setHours(0, 0, 0, 0);
    const slot = days.find((y) => y.d.getTime() === day.getTime());
    if (slot) slot.count++;
  });
  const max = Math.max(1, ...days.map((x) => x.count));
  $("activity-total").textContent = `${fmt(days.reduce((t, x) => t + x.count, 0))} hunts`;
  $("activity-chart").innerHTML = days.map((x, i) => `
    <div class="flex flex-col items-center justify-end gap-1 flex-1 h-full" title="${x.count} hunts completed">
      <span class="text-[10px] font-bold text-on-surface-variant">${x.count || ""}</span>
      <div class="bar w-full rounded-t-md ${i === 6 ? "bg-[#77c6fc]" : "bg-primary"}" style="height:${Math.max(3, (x.count / max) * 85)}%"></div>
      <span class="text-[10px] text-on-surface-variant font-medium">${x.d.toLocaleDateString("en-US", { weekday: "short" })}</span>
    </div>`).join("");

  // Recent hunt completions (read-only, written by the game)
  const names = Object.fromEntries(users.map((u) => [u.id, u.name]));
  const recent = [...completed].sort((a, b) => toDate(b.endTime) - toDate(a.endTime)).slice(0, 6);
  $("recent-completions").innerHTML = recent.length ? recent.map((x) => {
    const mins = toDate(x.startTime) ? Math.round((toDate(x.endTime) - toDate(x.startTime)) / 60000) : 0;
    return `
    <div class="flex items-center justify-between p-4 bg-surface-container-low rounded-lg gap-3">
      <div class="flex items-center gap-4 min-w-0">
        <div class="w-10 h-10 rounded-lg bg-primary/10 flex items-center justify-center shrink-0"><span class="material-symbols-outlined text-primary" style="font-size:20px">auto_stories</span></div>
        <div class="min-w-0">
          <p class="font-bold text-sm truncate">${esc(names[x.userId] || "Player")} completed ${esc(x.legend || "a hunt")}</p>
          <p class="text-xs text-on-surface-variant truncate">${[x.destination, mins > 0 && `${mins} min`, `${fmt(x.numSteps)} steps`, x.distance && `${num(x.distance).toFixed(2)} km`].filter(Boolean).map(esc).join(" · ")}</p>
        </div>
      </div>
      <div class="text-right shrink-0">
        <p class="text-sm font-bold text-primary">+${fmt(x.crystalsEarned)} 💎</p>
        <p class="text-[11px] text-on-surface-variant">${esc(timeAgo(x.endTime))}</p>
      </div>
    </div>`;
  }).join("") : emptyState("auto_stories", "No hunts completed yet. Completions from the game will appear here.");
}

function renderNotifications() {
  const open = state.flags.filter((f) => (f.status || "open") === "open")
    .sort((a, b) => (toDate(b.createdAt) || 0) - (toDate(a.createdAt) || 0));
  const badge = $("notif-badge");
  badge.textContent = open.length > 9 ? "9+" : open.length;
  badge.classList.toggle("hidden", open.length === 0);
  $("notif-list").innerHTML = open.length ? open.slice(0, 4).map((f) => `
    <div class="dropdown-item" data-nav="flags" style="flex-direction:column;align-items:flex-start;gap:2px">
      <p style="font-weight:600;font-size:13px;color:#191a2f">${esc(f.title)}</p>
      <p style="font-size:11px;color:#707880">${esc(f.userID ? `Player ${f.userID}` : "Walking session")} · ${esc(timeAgo(f.createdAt))}</p>
    </div>`).join("") : `<div class="dropdown-item">You're all caught up 🎉</div>`;
}

// ================= NETLIFY BLOB IMAGE UPLOADS =================
// Uploads require a Firebase sign-in and execute on Netlify Functions, not VS Code Live Server.
async function prepareImage(file) {
  if (!/^image\/(jpeg|png|webp)$/.test(file.type)) throw new Error("Choose a JPG, PNG or WebP photo.");
  if (file.size > 20 * 1024 * 1024) throw new Error("This photo is too large (20 MB maximum).");
  let bmp;
  try { bmp = await createImageBitmap(file); }
  catch { throw new Error("We couldn't open that photo. Try another image."); }
  const scale = Math.min(1, 1200 / Math.max(bmp.width, bmp.height));
  const canvas = document.createElement("canvas");
  canvas.width = Math.max(1, Math.round(bmp.width * scale));
  canvas.height = Math.max(1, Math.round(bmp.height * scale));
  canvas.getContext("2d").drawImage(bmp, 0, 0, canvas.width, canvas.height);
  bmp.close?.();
  const blob = await new Promise((resolve) => canvas.toBlob(resolve, "image/webp", 0.8));
  if (!blob || blob.size > 3 * 1024 * 1024) throw new Error("We couldn't resize that picture. Try a smaller photo.");
  return blob;
}
async function authorizedFetch(endpoint, options = {}) {
  const user = auth?.currentUser;
  if (!user) throw new Error("Please sign in first.");
  const token = await user.getIdToken();
  let response;
  try {
    response = await fetch(endpoint, { ...options, headers: { ...options.headers, Authorization: `Bearer ${token}` } });
  } catch {
    throw new Error("Couldn't reach the MasAR+ backend. Use Netlify Dev locally or the deployed Netlify site; Live Server cannot run image or AI functions.");
  }
  const body = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(body.error || (response.status === 404 ? "Backend endpoint not found. Run with Netlify Dev or deploy to Netlify; Live Server cannot process uploads." : `Request failed (${response.status}).`));
  return body;
}
async function uploadImage(blob, kind = "product") {
  const form = new FormData();
  form.append("image", blob, "masar-product.webp");
  form.append("kind", kind);
  const result = await authorizedFetch("/api/reward-images", { method: "POST", body: form });
  if (!result.url) throw new Error("Image hosting returned no URL. Try again.");
  return result.url;
}
// Existing images are intentionally not removed here: selected rewards and claims may still reference them.
async function deleteImage(url) { void url; }

function fileToDataURL(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result);
    reader.onerror = () => reject(new Error("Couldn't read the image."));
    reader.readAsDataURL(file);
  });
}
async function generateProductDescription() {
  if (!modalCfg || !modalCfg.fields.some((f) => f.name === "description")) return;
  const button = document.querySelector('[data-action="generate-description"]');
  const description = $("modal-form").elements.description;
  const title = $("modal-form").elements.title?.value?.trim();
  if (!title) return showToast("Enter the product name first.", true);
  const picture = modalImages.imageUrl;
  if (!picture || picture.removed || (!picture.file && !picture.original)) return showToast("Choose a product photo first.", true);
  const prior = button.textContent;
  button.disabled = true; button.textContent = "Generating…";
  try {
    let photo = picture.file;
    if (!photo) {
      const response = await fetch(picture.original);
      if (!response.ok) throw new Error("Please choose the product photo again before generating.");
      photo = await response.blob();
    }
    const imageData = await fileToDataURL(photo);
    const answer = await authorizedFetch("/api/generate-description", {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ title, rewardType: $("modal-form").elements.rewardType?.value || "gift", imageData }),
    });
    description.value = answer.description || description.value;
    showToast("Description generated. You can edit it before saving.");
  } catch (e) { showToast(friendlyError(e), true); }
  finally { button.disabled = false; button.textContent = prior; }
}

// ================= MODAL (rewards / sponsors / flags) =================
let modalCfg = null;
let modalImages = {}; // field name -> { original, file, previewUrl, removed }

function renderImagePreview(name) {
  const img = modalImages[name];
  const box = document.querySelector(`[data-img-preview="${name}"]`);
  if (!box) return;
  const src = img.previewUrl || (!img.removed && img.original) || "";
  box.innerHTML = src
    ? `<img src="${esc(src)}" alt="Reward image preview" class="w-full h-full object-cover"/>`
    : `<span class="material-symbols-outlined text-outline" style="font-size:32px">add_photo_alternate</span>`;
  document.querySelector(`[data-action="modal-img-remove"][data-field="${name}"]`)?.classList.toggle("hidden", !src);
}
function openModal(cfg) {
  modalCfg = cfg;
  Object.values(modalImages).forEach((m) => m.previewUrl && URL.revokeObjectURL(m.previewUrl));
  modalImages = {};
  $("modal-title").textContent = cfg.title;
  $("modal-error").classList.add("hidden");
  $("modal-delete").classList.toggle("hidden", !cfg.onDelete);
  $("modal-fields").innerHTML = cfg.fields.map((f) => {
    const v = f.value ?? "";
    let input;
    if (f.type === "select") input = `<select name="${f.name}" class="field">${f.options.map((o) => { const [val, lab] = Array.isArray(o) ? o : [o, o]; return `<option value="${esc(val)}" ${String(val) === String(v) ? "selected" : ""}>${esc(lab)}</option>`; }).join("")}</select>`;
    else if (f.type === "textarea") input = `<textarea name="${f.name}" rows="3" class="field resize-none" placeholder="${esc(f.placeholder || "")}">${esc(v)}</textarea>`;
    else if (f.type === "image") {
      modalImages[f.name] = { original: v, file: null, previewUrl: "", removed: false };
      return `<div class="flex flex-col gap-2"><label class="field-label">${esc(f.label)}</label>
        <div class="flex items-center gap-4">
          <label class="w-28 h-28 rounded-xl bg-surface-container-low overflow-hidden flex items-center justify-center cursor-pointer shrink-0 hover:ring-2 hover:ring-primary/30" data-img-preview="${f.name}">
          </label>
          <div class="flex flex-col gap-2">
            <label class="text-xs font-bold text-primary bg-primary/10 px-3 py-2 rounded-lg cursor-pointer hover:bg-primary/20 w-fit">
              <span class="material-symbols-outlined align-middle" style="font-size:16px">upload</span> Choose image
              <input type="file" name="${f.name}" data-image-input="${f.name}" accept="image/jpeg,image/png,image/webp" class="hidden"/>
            </label>
            <button type="button" data-action="modal-img-remove" data-field="${f.name}" class="text-xs font-bold text-error w-fit hover:underline">Remove image</button>
            <p class="text-[11px] text-on-surface-variant">JPG, PNG or WebP. Large photos are resized automatically.</p>
          </div>
        </div></div>`;
    }
    else if (f.type === "checkbox") return `<label class="flex items-center gap-3 font-semibold text-sm"><input type="checkbox" name="${f.name}" ${v ? "checked" : ""} class="rounded text-primary focus:ring-primary"/> ${esc(f.label)}</label>`;
    else input = `<input name="${f.name}" type="${f.type || "text"}" value="${esc(v)}" class="field" ${f.type === "number" ? `min="0" step="${f.step || "1"}"` : ""}/>`;
    return `<div class="flex flex-col gap-2"><label class="field-label">${esc(f.label)}${f.required ? " *" : ""}</label>${input}${f.generateButton ? `<button type="button" data-action="generate-description" class="text-xs font-bold text-primary bg-primary/10 px-3 py-2 rounded-lg hover:bg-primary/20 w-fit flex items-center gap-2"><span class="material-symbols-outlined" style="font-size:17px">auto_awesome</span> Generate description from photo</button><p class="text-[11px] text-on-surface-variant">Optional. Review the AI suggestion and edit it freely.</p>` : ""}</div>`;
  }).join("");
  Object.keys(modalImages).forEach((name) => {
    renderImagePreview(name);
    const dropZone = document.querySelector(`[data-img-preview="${name}"]`);
    dropZone.addEventListener("click", (e) => {
      e.preventDefault();
      document.querySelector(`[data-image-input="${name}"]`).click();
    });
    dropZone.addEventListener("dragover", (e) => { e.preventDefault(); dropZone.classList.add("ring-2", "ring-primary"); });
    dropZone.addEventListener("dragleave", () => dropZone.classList.remove("ring-2", "ring-primary"));
    dropZone.addEventListener("drop", (e) => {
      e.preventDefault(); dropZone.classList.remove("ring-2", "ring-primary");
      if (e.dataTransfer.files[0]) setModalImage(name, e.dataTransfer.files[0]);
    });
  });
  $("modal").classList.add("open");
  $("modal-fields").querySelector("input:not([type=file]),select,textarea")?.focus();
}
function closeModal() { $("modal").classList.remove("open"); modalCfg = null; }

async function onModalImageChosen(input) {
  const name = input.dataset.imageInput;
  const file = input.files?.[0];
  input.value = "";
  await setModalImage(name, file);
}
async function setModalImage(name, file) {
  if (!file || !modalImages[name]) return;
  try {
    const blob = await prepareImage(file);
    const img = modalImages[name];
    if (img.previewUrl) URL.revokeObjectURL(img.previewUrl);
    img.file = blob;
    img.previewUrl = URL.createObjectURL(blob);
    img.removed = false;
    $("modal-error").classList.add("hidden");
    renderImagePreview(name);
  } catch (e) {
    $("modal-error").textContent = e.message;
    $("modal-error").classList.remove("hidden");
  }
}

async function submitModal(e) {
  e.preventDefault();
  if (!modalCfg) return;
  const form = $("modal-form");
  const values = {};
  for (const f of modalCfg.fields) {
    if (f.type === "image") continue;
    const el = form.elements[f.name];
    let v = f.type === "checkbox" ? el.checked : el.value.trim();
    if (f.type === "number") v = num(v);
    if (f.type === "date") v = v || null;
    if (f.required && !v) {
      $("modal-error").textContent = `${f.label} is required.`;
      $("modal-error").classList.remove("hidden");
      return;
    }
    values[f.name] = v;
  }
  const btn = $("modal-save");
  setBusy(btn, true);
  const replaced = [];
  try {
    for (const f of modalCfg.fields.filter((x) => x.type === "image")) {
      const img = modalImages[f.name];
      if (img.file) {
        btn.textContent = "Uploading image…";
        values[f.name] = await uploadImage(img.file, "product");
      } else {
        values[f.name] = img.removed ? "" : img.original || "";
      }
      if (img.original && values[f.name] !== img.original) replaced.push(img.original);
    }
    btn.textContent = "Saving…";
    await modalCfg.onSave(values);
    replaced.forEach(deleteImage); // clean up the old file after the record no longer points at it
    closeModal();
  } catch (err) {
    $("modal-error").textContent = friendlyError(err);
    $("modal-error").classList.remove("hidden");
  } finally { setBusy(btn, false); }
}

async function saveDoc(col, id, values, msg) {
  const data = { ...values, updatedAt: serverTimestamp() };
  if (id) await updateDoc(doc(db, col, id), data);
  else await addDoc(collection(db, col), { ...data, createdAt: serverTimestamp() });
  showToast(msg);
}
async function removeDoc(col, id, msg, reload) {
  if (!confirm("Delete this item permanently?")) return false;
  try { await deleteDoc(doc(db, col, id)); closeModal(); showToast(msg); reload(); return true; }
  catch (err) { showToast(friendlyError(err), true); return false; }
}

// ================= REWARDS =================

async function loadRewards() {
  $("reward-grid").innerHTML = skeleton(3, "h-44");

  [state.rewards, state.partners] = await Promise.all([
    list("rewards"),
    listSafe("partners"),
  ]);

  await loadAdminProductCatalog();

  const styles = [
    ["amber", "emoji_events"],
    ["blue", "card_giftcard"],
    ["purple", "fitness_center"],
    ["green", "local_cafe"],
    ["rose", "redeem"],
  ];

  $("reward-grid").innerHTML = state.rewards.length
    ? state.rewards
        .sort((a, b) => rewardCost(b) - rewardCost(a))
        .map((r, i) => {
          const [c, icon] = styles[i % styles.length];

          const partner = state.partners.find(
            (p) => p.id === r.partnerId
          );

          // Use the inventory counters stored on the
          // reward document. Flutter updates redeemedCount
          // atomically when a player redeems a reward.
          const total =
            Number.isInteger(r.quantityTotal) &&
            r.quantityTotal >= 0
              ? r.quantityTotal
              : null;

          const redeemed =
            Number.isInteger(r.redeemedCount) &&
            r.redeemedCount >= 0
              ? r.redeemedCount
              : 0;

          const remaining =
            total === null
              ? null
              : Math.max(0, total - redeemed);

          const outOfStock = remaining === 0;

          const stockPercent =
            total !== null && total > 0
              ? Math.min(
                  100,
                  (redeemed / total) * 100
                )
              : 0;

          return `
          <div class="quest-card bg-surface-container-lowest rounded-xl shadow-sm flex flex-col overflow-hidden">

            ${
              r.imageUrl
                ? `<div class="relative h-40 bg-surface-container-low">
                    <img
                      src="${esc(r.imageUrl)}"
                      alt="${esc(r.title)}"
                      class="w-full h-full object-cover"
                      loading="lazy"
                      onerror="this.parentElement.remove()"
                    />
                    <div class="absolute top-3 right-3">
                      ${statusPill(r.availStatus)}
                    </div>
                  </div>`
                : ""
            }

            <div class="p-6 flex flex-col flex-1">

              ${
                r.imageUrl
                  ? ""
                  : `<div class="flex items-center justify-between mb-4">
                      <div class="w-12 h-12 rounded-xl bg-${c}-100 flex items-center justify-center">
                        <span
                          class="material-symbols-outlined text-${c}-600 fill-icon"
                          style="font-size:24px"
                        >${icon}</span>
                      </div>
                      ${statusPill(r.availStatus)}
                    </div>`
              }

              <h4 class="font-headline font-bold text-base mb-1">
                ${esc(r.title)}
              </h4>

              ${
                r.rewardType
                  ? `<p class="text-[11px] font-bold uppercase text-primary mb-1">
                      ${esc(r.rewardType)}
                    </p>`
                  : ""
              }

              <p class="text-xs text-on-surface-variant mb-2">
                ${esc(r.description)}
              </p>

              ${
                r.conditions
                  ? `<p class="text-xs text-on-surface mb-2 flex gap-1">
                      <span
                        class="material-symbols-outlined text-primary"
                        style="font-size:14px"
                      >rule</span>
                      <span>${esc(r.conditions)}</span>
                    </p>`
                  : ""
              }

              <div class="mt-4 mb-4 rounded-xl bg-surface-container-low p-4">

                <div class="grid grid-cols-3 gap-2 text-center">

                  <div>
                    <p class="text-[10px] text-on-surface-variant font-bold uppercase">
                      Total
                    </p>
                    <p class="font-headline text-lg font-extrabold">
                      ${total === null ? "—" : fmt(total)}
                    </p>
                  </div>

                  <div>
                    <p class="text-[10px] text-on-surface-variant font-bold uppercase">
                      Redeemed
                    </p>
                    <p class="font-headline text-lg font-extrabold">
                      ${fmt(redeemed)}
                    </p>
                  </div>

                  <div>
                    <p class="text-[10px] text-on-surface-variant font-bold uppercase">
                      Remaining
                    </p>
                    <p class="font-headline text-lg font-extrabold ${
                      outOfStock ? "text-red-700" : "text-primary"
                    }">
                      ${
                        remaining === null
                          ? "—"
                          : fmt(remaining)
                      }
                    </p>
                  </div>

                </div>

                ${
                  total !== null
                    ? `<div class="w-full h-2 bg-white rounded-full overflow-hidden mt-3">
                        <div
                          class="h-full bg-primary rounded-full"
                          style="width:${stockPercent}%"
                        ></div>
                      </div>`
                    : ""
                }

                ${
                  outOfStock
                    ? `<p class="text-red-700 text-xs font-bold mt-3 text-center">
                        Out of stock
                      </p>`
                    : remaining === null
                    ? `<p class="text-amber-700 text-xs font-bold mt-3 text-center">
                        Inventory not configured
                      </p>`
                    : `<p class="text-xs text-on-surface-variant mt-3 text-center">
                        ${fmt(remaining)} units available
                      </p>`
                }

              </div>

              <div class="flex-1"></div>

              <p class="text-[11px] text-on-surface-variant mb-4">
                ${
                  partner
                    ? `By ${esc(partner.businessName)}`
                    : ""
                }
                ${
                  r.expDate
                    ? ` · Expires ${esc(fmtDate(r.expDate))}`
                    : ""
                }
              </p>

              <div class="flex items-center justify-between">

                <span class="font-headline font-extrabold text-2xl text-primary">
                  ${fmt(rewardCost(r))}
                  <span class="text-sm font-body font-normal text-on-surface-variant">
                    crystals
                  </span>
                </span>

                <div class="flex flex-col gap-1 text-right">

                  <button
                    data-action="edit-reward"
                    data-id="${esc(r.id)}"
                    class="text-xs font-bold text-primary hover:underline"
                  >
                    ${r.sourceProductId ? "Visibility" : "Edit"}
                  </button>

                  ${
                    r.sourceProductId
                      ? `<button
                          data-action="admin-edit-product"
                          data-id="${esc(r.sourceProductId)}"
                          class="text-xs font-bold text-primary hover:underline"
                        >
                          Edit details
                        </button>`
                      : ""
                  }

                </div>
              </div>

            </div>
          </div>`;
        })
        .join("")
    : emptyState(
        "military_tech",
        "No rewards yet. Add your first reward."
      );
}



function rewardModal(r = {}) {
  // Sponsor reward prices derive from KD value and cannot be edited independently.
  if (r.sourceProductId) {
    openModal({
      title: `Reward Visibility — ${r.title}`,
      fields: [
        { name: "availStatus", label: "Published to app", type: "select", options: ["Active", "Draft"], value: r.availStatus || "Active" },
      ],
      onSave: async (v) => {
        await updateDoc(doc(db, "rewards", r.id), { availStatus: v.availStatus, updatedAt: serverTimestamp() });
        showToast(v.availStatus === "Active" ? "Reward published" : "Reward unpublished");
        loadRewards();
      },
    });
    return;
  }

  openModal({
    title: r.id ? "Edit Reward" : "Add Reward",
    fields: [
      { name: "imageUrl", label: "Reward image", type: "image", value: r.imageUrl || "" },
      { name: "title", label: "Title", required: true, value: r.title },
      { name: "description", label: "Description", type: "textarea", value: r.description },
      { name: "conditions", label: "Conditions / requirements", type: "textarea", required: true, value: r.conditions, placeholder: "e.g. Reach level 5 and keep a 7-day streak. One per player." },
      { name: "crystalCost", label: "Cost (crystals)", type: "number", required: true, value: r.crystalCost ?? r.points ?? "" },
      {name: "quantityTotal",  label: "Total quantity available", type: "number", required: true, value: r.quantityTotal ?? "",},
      { name: "availStatus", label: "Status", type: "select", options: ["Active", "Draft", "Expired"], value: r.availStatus || "Active" },
      { name: "expDate", label: "Expiry date", type: "date", value: r.expDate || "" },
      { name: "partnerId", label: "Partner", type: "select", options: [["", "None"], ...state.partners.filter((p) => !["pending", "rejected"].includes(p.status)).map((p) => [p.id, p.businessName])], value: r.partnerId || "" },
    ],
    
  onSave: async (v) => {
    const redeemed = Number.isInteger(r.redeemedCount)
      ? r.redeemedCount
      : 0;

    if (
      !Number.isInteger(v.quantityTotal) ||
      v.quantityTotal <= 0
    ) {
      throw new Error(
        "Total quantity must be a positive whole number."
      );
    }

    if (v.quantityTotal < redeemed) {
      throw new Error(
        "Total quantity cannot be less than the number already redeemed."
      );
    }

  await saveDoc(
    "rewards",
    r.id,
    {
      ...v,
      ...(r.id ? {} : { redeemedCount: 0 }),
      adminId: admin.uid,
    },
    "Reward saved"
  );

  loadRewards();
},

  onDelete: r.id ? async () => { if (await removeDoc("rewards", r.id, "Reward deleted", loadRewards)) deleteImage(r.imageUrl); } : null,
  });
}

// ================= SPONSORS =================
async function loadSponsors() {
  $("partner-list").innerHTML = skeleton(3);
  state.partners = await list("partners");
  const visiblePartners = state.partners;
  const soon = Date.now() + 60 * 864e5;
  const isDue = (p) => { const d = toDate(p.renewalDate); return d && d.getTime() <= soon; };
  $("sp-total").textContent = fmt(visiblePartners.length);
  $("sp-verified").textContent = fmt(visiblePartners.filter((p) => p.verified).length);
  $("sp-renewals").textContent = fmt(visiblePartners.filter(isDue).length);
  const colors = ["blue", "green", "purple", "amber", "rose"];
  $("partner-list").innerHTML = visiblePartners.length ? visiblePartners
    .sort((a, b) => (a.businessName || "").localeCompare(b.businessName || ""))
    .map((p, i) => {
      const c = colors[i % colors.length];
      const pill = p.status === "suspended" || p.status === "rejected" ? `<span class="text-xs font-bold text-red-700 bg-red-100 px-2 py-1 rounded-full">Suspended</span>` : isDue(p) ? `<span class="text-xs font-bold text-amber-700 bg-amber-100 px-2 py-1 rounded-full">⚠ Renewal Due</span>`
        : p.verified ? `<span class="text-xs font-bold text-green-700 bg-green-100 px-2 py-1 rounded-full">Verified</span>`
        : `<span class="text-xs font-bold text-slate-600 bg-slate-200 px-2 py-1 rounded-full">Unverified</span>`;
      return `
      <div class="flex items-center justify-between p-4 bg-surface-container-low rounded-lg gap-3">
        <div class="flex items-center gap-4 min-w-0">
          <div class="w-10 h-10 rounded-lg bg-${c}-100 flex items-center justify-center font-black text-${c}-600 text-sm shrink-0">${esc(initials(p.businessName))}</div>
          <div class="min-w-0"><p class="font-bold text-sm truncate">${esc(p.businessName)}</p><p class="text-xs text-on-surface-variant truncate">${p.tier ? `Tier: ${esc(p.tier)}` : esc(p.contactInfo)}${p.renewalDate ? ` · Renews ${esc(fmtDate(p.renewalDate, { month: "short", year: "numeric" }))}` : ""}</p></div>
        </div>
        <div class="flex items-center gap-3 shrink-0">${pill}<button data-action="edit-partner" data-id="${esc(p.id)}" class="text-xs text-primary font-semibold hover:underline">Manage</button>${p.ownerUid ? `<button data-action="toggle-partner" data-id="${esc(p.id)}" class="text-xs font-bold ${p.status === "suspended" ? "text-green-700" : "text-error"}">${p.status === "suspended" ? "Reactivate" : "Suspend"}</button>` : ""}</div>
      </div>`;
    }).join("") : emptyState("handshake", "No partners yet.");
}

function partnerModal(p = {}) {
  openModal({
    title: p.id ? "Manage Partner" : "Add Partner",
    fields: [
      { name: "businessName", label: "Business name", required: true, value: p.businessName },
      { name: "description", label: "Description", type: "textarea", value: p.description },
      { name: "contactInfo", label: "Contact info", value: p.contactInfo },
      { name: "pickupLocation", label: "Reward pickup location", value: p.pickupLocation || "" },
      { name: "tier", label: "Tier", type: "select", options: ["Platinum", "Gold", "Silver", "Bronze"], value: p.tier || "Silver" },
      { name: "renewalDate", label: "Renewal date", type: "date", value: p.renewalDate || "" },
      { name: "verified", label: "Verified partner", type: "checkbox", value: !!p.verified },
    ],
    onSave: async (v) => { await saveDoc("partners", p.id, v, "Partner saved"); loadSponsors(); },
    onDelete: p.id ? () => removeDoc("partners", p.id, "Partner deleted", loadSponsors) : null,
  });
}

// ================= SPONSOR PORTAL EXTENSION =================
// All sponsor views reuse the original MasAR+ page, typography, cards and modal.
// Firestore access control MUST be enforced by firestore.rules, never by these views alone.
let sponsorProducts = [];
let sponsorRewards = [];
let adminProducts = [];

const kwd = (fils) => `KD ${(num(fils) / 1000).toFixed(3)}`;
const filsFromKwd = (value) => {
  const v = Number(value);
  if (!Number.isFinite(v) || v < 0 || v > 100000) throw new Error("Enter a valid non-negative amount in KD.");
  return Math.round(v * 1000);
};
const rewardValueLabel = (p) => kwd(p.valueFils);
const crystalPrice = (valueFils) => {
  if (!Number.isInteger(valueFils) || valueFils < 1) throw new Error("A reward must have a positive value in fils.");
  // 1 KWD = 1000 fils, 1 KWD = 1000 crystals, therefore 1 fils = 1 crystal.
  return valueFils;
};
const productStatus = (p) => p.status === "selected" ? "Selected" : p.status === "rejected" ? "Declined" : p.status === "unavailable" ? "Unavailable" : "Available";
const sponsorImage = (p, size = "h-36") => p.imageUrl
  ? `<img src="${esc(p.imageUrl)}" alt="${esc(p.title)}" class="w-full ${size} object-cover rounded-lg" loading="lazy">`
  : `<div class="w-full ${size} rounded-lg bg-primary-fixed flex items-center justify-center"><span class="material-symbols-outlined text-primary" style="font-size:40px">redeem</span></div>`;


async function ownedDocuments(name) {
  if (!sponsorPartnerId) throw new Error("Your sponsor company isn't approved yet.");
  const q = query(collection(db, name), where("partnerId", "==", sponsorPartnerId));
  const snapshot = await getDocs(q);
  return snapshot.docs.map((d) => ({ id: d.id, ...d.data() }));
}

async function resolveAccount(user) {
  const adminSnap = await getDoc(doc(db, "admins", user.uid));
  if (adminSnap.exists()) {
    accountRole = "admin";
    sponsorPartnerId = null;
    admin = { uid: user.uid, email: user.email, displayName: user.displayName, ...adminSnap.data() };
    renderProfile();
    $("boot").classList.add("hidden");
    const [page, param] = location.hash.slice(1).split("/");
    navigate(PAGES.includes(page) ? page : "dashboard", param);
    return;
  }
  // One document per registered sponsor: partners/{firebaseAuthUid}.
  // Old manually-created partners without an ownerUid remain admin-managed only.
  const partnerSnap = await getDoc(doc(db, "partners", user.uid));
  if (!partnerSnap.exists() || partnerSnap.data().ownerUid !== user.uid) {
    await signOut(auth);
    showLoginMessage("error", "No administrator or sponsor application was found for this account.");
    return;
  }
  const company = partnerSnap.data();
  const approved = company.status === "active" || (company.status === "approved" && company.verified === true);
  sponsorPartnerId = approved ? user.uid : null;
  accountRole = approved ? "sponsor" : "pending";
  admin = {
    uid: user.uid, email: user.email,
    username: approved ? company.businessName : (company.contactName || company.businessName),
    role: approved ? "Sponsor" : "Sponsor Applicant",
  };
  renderProfile();
  $("boot").classList.add("hidden");
  if (approved) {
    const page = location.hash.slice(1);
    navigate(SPONSOR_PAGES.includes(page) ? page : "sp-dashboard");
    return;
  }
  const rejected = ["rejected", "suspended"].includes(company.status);
  $("application-status-title").textContent = rejected ? "Company account suspended" : "Account setup incomplete";
  $("application-status-info").textContent = rejected
    ? (company.rejectionReason || "Your company account is suspended. Contact MasAR+ if you need help.")
    : `${company.businessName} could not be activated. Contact MasAR+ support.`;
  navigate("application-status");
}

async function doSponsorSignup(e) {
  e.preventDefault();
  const form = e.currentTarget;
  const values = Object.fromEntries(new FormData(form).entries());
  const error = $("signup-error"), btn = $("signup-submit");
  error.classList.add("hidden");
  if (!form.reportValidity()) return;
  if (values.password.length < 8) { error.textContent = "Choose a password with at least 8 characters."; error.classList.remove("hidden"); return; }
  setBusy(btn, true, "Creating your account…");
  registeringSponsor = true;
  let createdUser = null;
  try {
    const credential = await createUserWithEmailAndPassword(auth, String(values.email).trim(), values.password);
    createdUser = credential.user;
    // Active from registration; no administrator approval or second collection.
    await setDoc(doc(db, "partners", createdUser.uid), {
      ownerUid: createdUser.uid,
      businessName: String(values.businessName).trim(),
      contactName: String(values.contactName).trim(),
      email: createdUser.email, contactInfo: createdUser.email,
      phone: String(values.phone).trim(), industry: String(values.industry).trim(),
      website: String(values.website || "").trim(),
      pickupLocation: String(values.pickupLocation || "").trim(),
      description: String(values.description).trim(), logoUrl: "",
      status: "active", verified: false,
      createdAt: serverTimestamp(), updatedAt: serverTimestamp(),
    });
    // Logo failure never blocks registration. The company can still submit products.
    let logoProblem = false;
    const logo = form.elements.logo.files[0];
    if (logo) {
      btn.textContent = "Uploading your logo…";
      try {
        const logoUrl = await uploadImage(await prepareImage(logo), "logo");
        await updateDoc(doc(db, "partners", createdUser.uid), { logoUrl, updatedAt: serverTimestamp() });
      } catch (logoError) { logoProblem = true; console.warn("Optional logo upload failed:", logoError); }
    }
    form.reset();
    showToast(logoProblem ? "Account ready! Logo upload failed, but you can add products." : "Welcome! Your sponsor account is ready.", logoProblem);
    await resolveAccount(createdUser);
  } catch (err) {
    console.error(err);
    error.textContent = createdUser
      ? `Account created but company setup failed. Please contact MasAR+ with your email. (${friendlyError(err)})`
      : friendlyError(err);
    error.classList.remove("hidden");
    if (createdUser) await signOut(auth);
  } finally { registeringSponsor = false; setBusy(btn, false); }
}

function productModalFields(p = {}) {
  return [
    { name: "imageUrl", label: "Product photo", type: "image", value: p.imageUrl || "" },
    { name: "title", label: "Product name", required: true, value: p.title },
    { name: "rewardType", label: "Reward type", type: "select", options: [["gift", "Gift"], ["voucher", "Voucher"]], value: p.rewardType || "gift" },
    { name: "valueKwd", label: "Value in KD", type: "number", step: "0.001", required: true, value: p.valueFils ? (num(p.valueFils) / 1000).toFixed(3) : "" },
    { name: "quantityOffered", label: "Quantity", type: "number", required: true, value: p.quantityOffered || 1 },
    { name: "description", label: "Description", type: "textarea", required: true, generateButton: true, value: p.description, placeholder: "Write a short description or use Generate description." },
  ];
}
function parseProductForm(v) {
  const valueFils = filsFromKwd(v.valueKwd);
  const quantityOffered = Number(v.quantityOffered);
  if (!Number.isInteger(quantityOffered) || quantityOffered < 1 || quantityOffered > 1000000) throw new Error("Enter a quantity from 1 to 1,000,000.");
  if (!["gift", "voucher"].includes(v.rewardType)) throw new Error("Choose Gift or Voucher.");
  if (valueFils < 1 || !/^\d+(?:\.\d{1,3})?$/.test(String(v.valueKwd))) throw new Error("Enter the value in KD with up to three decimal places.");
  const title = String(v.title || "").trim();
  const description = String(v.description || "").trim();
  if (!title || title.length > 140 || !description || description.length > 2000) throw new Error("Enter a valid product name and description.");
  return { imageUrl: v.imageUrl || "", title, description, rewardType: v.rewardType,
    valueFils, quantityOffered };
}
function sponsorProductModal(p = {}) {
  if (accountRole !== "sponsor" || p.status === "selected" || p.status === "rejected") {
    if (p.status) showToast("This product is locked after selection or an admin decline.", true);
    return;
  }
  openModal({
    title: p.id ? "Edit Product" : "Offer a New Product",
    fields: productModalFields(p),
    onSave: async (v) => {
      const fields = parseProductForm(v);
      const data = { ...fields, partnerId: sponsorPartnerId,
        createdBy: auth.currentUser.uid, status: "available", updatedAt: serverTimestamp() };
      if (p.id) await updateDoc(doc(db, "sponsor_products", p.id), data);
      else await addDoc(collection(db, "sponsor_products"), { ...data, createdAt: serverTimestamp() });
      showToast(p.id ? "Product updated" : "Product listed. No approval required.");
      loadSponsorProducts();
    },
  });
}

async function loadSponsorProducts() {
  sponsorProducts = await ownedDocuments("sponsor_products");
  const cards = sponsorProducts.slice().sort((a, b) => (toDate(b.createdAt) || 0) - (toDate(a.createdAt) || 0));
  $("sp-product-list").innerHTML = cards.length ? cards.map((p) => `
    <div class="quest-card bg-surface-container-lowest rounded-xl p-5 shadow-sm flex flex-col gap-3">
      ${sponsorImage(p)}
      <div class="flex justify-between gap-2 items-center"><h3 class="font-headline font-bold text-lg">${esc(p.title)}</h3>${statusPill(productStatus(p))}</div>
      <p class="text-sm text-on-surface-variant">${esc(p.rewardType)} · ${esc(rewardValueLabel(p))} · ${fmt(p.quantityOffered)} offered</p>
      <p class="text-xs text-on-surface-variant">${esc(p.description)}</p>
      <button data-action="edit-product" data-id="${esc(p.id)}" ${["selected","rejected"].includes(p.status) ? "disabled" : ""} class="text-sm text-primary font-bold self-start mt-auto disabled:opacity-50">${p.status === "selected" ? "Selected — Locked" : p.status === "rejected" ? "Declined — Locked" : "Edit Product"}</button>

    </div>`).join("") : emptyState("inventory_2", "No products yet. Add your first gift or voucher.");
}

async function loadSponsorDashboard() {
  await loadSponsorProducts();
  const selected = sponsorProducts.filter((p) => p.status === "selected");
  const total = sponsorProducts.reduce((a, p) => a + num(p.quantityOffered), 0);
  $("sp-overview").innerHTML = [
    ["Products Offered", sponsorProducts.length, "inventory_2"],
    ["Selected by Admin", selected.length, "verified"],
    ["Total Units Offered", total, "redeem"],
  ].map(([title, value, icon]) => `<div class="p-6 rounded-xl bg-white shadow-sm"><div class="text-on-surface-variant flex gap-2 text-sm items-center"><span class="material-symbols-outlined text-primary">${icon}</span>${title}</div><div class="font-headline text-3xl font-extrabold mt-3">${fmt(value)}</div></div>`).join("");
  $("sp-recent").innerHTML = sponsorProducts.slice(0, 5).map((p) => `<div class="py-3 border-b border-slate-100 flex justify-between gap-3"><span class="font-bold">${esc(p.title)}</span>${statusPill(productStatus(p))}</div>`).join("") || "No products yet.";
}

async function loadSponsorRewards() {
  sponsorProducts = await ownedDocuments("sponsor_products");
  sponsorRewards = (await list("rewards")).filter((r) => r.partnerId === sponsorPartnerId && !!r.sourceProductId);
  $("sp-selected-list").innerHTML = sponsorRewards.length ? sponsorRewards.map((r) => `
    <div class="quest-card bg-white rounded-xl shadow-sm p-5 space-y-3">
      ${sponsorImage(r)}<h3 class="font-headline text-lg font-bold">${esc(r.title)}</h3>
      <p class="text-sm text-on-surface-variant">${esc(r.rewardType || "gift")} · ${fmt(r.crystalCost)} crystals</p>
      <div class="text-sm">Allocated stock: <strong>${fmt(r.quantityTotal)}</strong></div>
      <div class="text-sm">Claims issued: <strong>${fmt(r.redeemedCount || 0)}</strong></div>
      <div class="text-sm">Available: <strong>${fmt(Math.max(0, num(r.quantityTotal) - num(r.redeemedCount)))}</strong></div>
      <div class="text-sm">Collected in person: <strong>${fmt(r.fulfilledCount || 0)}</strong></div>
      ${statusPill(r.availStatus)}
      <p class="text-xs text-on-surface-variant">Selected product details are locked. Contact an admin for corrections.</p>
    </div>`).join("") : emptyState("redeem", "The administrator has not published any of your products yet.");
}

async function verifyClaim(e) {
  e.preventDefault();
  const code = $("claim-code").value.trim().toUpperCase().replace(/\s+/g, "");
  if (!/^[A-F0-9]{24}$/.test(code)) return showToast("Enter the 24-character claim code shown by the player.", true);
  const button = $("claim-verify-btn");
  setBusy(button, true, "Verifying…");
  try {
    const result = await authorizedFetch("/api/fulfill-reward", {
      method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ code }),
    });
    $("claim-verify-result").innerHTML = `<div class="p-4 rounded-lg bg-green-50 text-green-900"><p class="font-bold">Verified — hand the ${esc(result.rewardType)} to the player</p><p class="text-sm mt-1">${esc(result.title)}</p></div>`;
    $("claim-code").value = "";
    showToast("Claim verified and marked used.");
  } catch (err) { $("claim-verify-result").textContent = ""; showToast(friendlyError(err), true); }
  finally { setBusy(button, false); }
}


async function loadSponsorProfile() {
  const snap = await getDoc(doc(db, "partners", sponsorPartnerId));
  if (!snap.exists()) throw new Error("Company profile not found.");
  const p = snap.data();
  $("sp-company-details").innerHTML = `
    <div class="flex items-start gap-5 mb-5">${p.logoUrl ? `<img src="${esc(p.logoUrl)}" alt="Company logo" class="h-20 w-20 rounded-xl object-contain bg-slate-50">` : `<div class="h-20 w-20 rounded-xl bg-primary-fixed flex items-center justify-center font-extrabold">${esc(initials(p.businessName))}</div>`}
    <div><h3 class="font-headline text-2xl font-bold">${esc(p.businessName)}</h3>${statusPill(["active", "approved"].includes(p.status) ? "Active" : "Suspended")}</div></div>
    <p class="text-on-surface-variant mb-5">${esc(p.description || "")}</p>
    <p class="text-sm"><strong>Industry:</strong> ${esc(p.industry || "—")}</p>
    <p class="text-sm mt-2"><strong>Website:</strong> ${esc(p.website || "—")}</p>
    <p class="text-sm mt-2"><strong>Reward collection location:</strong> ${esc(p.pickupLocation || "Contact the sponsor for pickup details")}</p>
    <p class="text-xs text-on-surface-variant mt-5">To change official company details, contact the MasAR+ administrator.</p>`;
}

async function toggleSponsorAccount(uid) {
  const p = state.partners.find((x) => x.id === uid);
  if (!p?.ownerUid || accountRole !== "admin") return;
  const active = p.status !== "suspended";
  if (!confirm(`${active ? "Suspend" : "Reactivate"} ${p.businessName}?`)) return;
  await updateDoc(doc(db, "partners", uid), {
    status: active ? "suspended" : "active",
    updatedAt: serverTimestamp(),
  });
  showToast(active ? "Sponsor suspended" : "Sponsor reactivated");
  loadSponsors();
}


async function loadAdminProductCatalog() {
  adminProducts = await listSafe("sponsor_products");
  const available = adminProducts.filter((p) => p.status === "available");
  $("admin-product-catalog").innerHTML = available.length ? available.map((p) => `
    <div class="quest-card bg-surface-container-low rounded-xl p-4 flex flex-col gap-3">
      ${sponsorImage(p, "h-28")}
      <div><p class="font-bold">${esc(p.title)}</p><p class="text-xs text-on-surface-variant">${esc(p.rewardType)} · ${esc(rewardValueLabel(p))} = <strong>${fmt(crystalPrice(p.valueFils))} crystals</strong></p><p class="text-xs text-on-surface-variant">${fmt(p.quantityOffered)} offered · ${esc(state.partners.find((c) => c.id === p.partnerId)?.businessName || p.partnerId)}</p></div>
      <div class="flex gap-2 mt-auto"><button data-action="select-product" data-id="${esc(p.id)}" class="bg-primary text-white font-bold px-4 py-2 rounded-lg text-xs flex-1">Publish ${fmt(p.quantityOffered)} units</button><button data-action="admin-edit-product" data-id="${esc(p.id)}" class="text-xs font-bold text-primary px-2">Edit</button><button data-action="deny-product" data-id="${esc(p.id)}" class="text-xs font-bold text-error px-2">Deny</button></div>
    </div>`).join("") : `<p class="text-sm text-on-surface-variant col-span-full">No new sponsor products waiting for selection.</p>`;
}

async function adminSelectProduct(id) {
  if (accountRole !== "admin") return;
  const p = adminProducts.find((x) => x.id === id);
  if (!p || p.status !== "available") return showToast("This product is not available for publishing.", true);
  const cost = crystalPrice(p.valueFils);
  if (!confirm(`Publish ${p.title} (${p.quantityOffered} units) for ${cost.toLocaleString()} crystals each?\n\nThe price is calculated automatically from ${kwd(p.valueFils)}.`)) return;
  const productRef = doc(db, "sponsor_products", id);
  const rewardRef = doc(db, "rewards", `sponsor_${id}`);
  try {
    await runTransaction(db, async (tx) => {
      const [productSnap, rewardSnap] = await Promise.all([tx.get(productRef), tx.get(rewardRef)]);
      if (!productSnap.exists() || productSnap.data().status !== "available") throw new Error("Product was changed or already selected.");
      if (rewardSnap.exists()) throw new Error("This product has already been published.");
      const item = productSnap.data();
      tx.set(rewardRef, {
        title: item.title, description: item.description, imageUrl: item.imageUrl,
        partnerId: item.partnerId, sourceProductId: id, rewardType: item.rewardType,
        crystalCost: crystalPrice(item.valueFils),
        conditions: "Redeem using crystals, then show your one-time claim code at the sponsor location.",
        redemptionMethod: "in_person_code",
        availStatus: "Active", quantityTotal: item.quantityOffered,
        redeemedCount: 0, fulfilledCount: 0, adminId: auth.currentUser.uid,
        createdAt: serverTimestamp(), updatedAt: serverTimestamp(),
      });
      tx.update(productRef, { status: "selected", updatedAt: serverTimestamp() });
    });
    showToast("Reward published with automatic crystal pricing.");
    loadRewards();
  } catch (err) { showToast(friendlyError(err), true); }
}
async function adminDenyProduct(id) {
  const p = adminProducts.find((x) => x.id === id);
  if (!p || accountRole !== "admin" || p.status !== "available") return;
  if (!confirm(`Decline ${p.title}? This removes it from the publishable catalog.`)) return;
  await updateDoc(doc(db, "sponsor_products", id), { status: "rejected", updatedAt: serverTimestamp() });
  showToast("Product declined"); loadRewards();
}
async function adminEditProduct(id) {
  if (accountRole !== "admin") return;
  const p = adminProducts.find((x) => x.id === id);
  if (!p) return;
  openModal({
    title: `Admin Edit — ${p.title}`,
    fields: productModalFields(p),
    onSave: async (v) => {
      const changes = parseProductForm(v);
      const productRef = doc(db, "sponsor_products", id);
      const rewardRef = doc(db, "rewards", `sponsor_${id}`);
      await runTransaction(db, async (tx) => {
        const productSnap = await tx.get(productRef);
        const rewardSnap = await tx.get(rewardRef);
        if (!productSnap.exists()) throw new Error("Product no longer exists.");
        const selected = productSnap.data().status === "selected";
        if (selected && !rewardSnap.exists()) throw new Error("The published reward is missing.");
        if (selected && changes.quantityOffered < num(rewardSnap.data().redeemedCount)) throw new Error("Quantity cannot be below the claims already issued.");
        tx.update(productRef, { ...changes, updatedAt: serverTimestamp() });
        if (selected) tx.update(rewardRef, {
          title: changes.title, description: changes.description, imageUrl: changes.imageUrl,
          rewardType: changes.rewardType, crystalCost: crystalPrice(changes.valueFils),
          quantityTotal: changes.quantityOffered, updatedAt: serverTimestamp(),
        });
      });
      showToast("Product updated. Crystal price recalculated automatically.");
      loadRewards();
    },
  });
}



// ================= FLAGS (automatic, from walking_sessions) =================
// Flags are detected in the browser from sessions the game saves. A document in
// /flags/{sessionId} is only written once an admin acts on it, storing the decision.
const FLAG_FIELDS = ["sessionID", "userID", "type", "title", "severity", "status", "details", "legend", "note", "handledBy", "createdAt", "updatedAt"];

function detectFlags(sessions) {
  const out = [];
  for (const x of sessions) {
    const a = toDate(x.startTime), b = toDate(x.endTime);
    if (!a || !b) continue;
    const mins = (b - a) / 60000, km = num(x.distance), steps = num(x.numSteps);
    let f = null;
    if (mins <= 0) f = { type: "invalid_time", severity: "high", title: "Invalid session time", details: "Session ends before (or when) it starts." };
    else if (mins > 360) f = { type: "invalid_time", severity: "medium", title: "Unusually long session", details: `Session lasted ${(mins / 60).toFixed(1)} hours.` };
    else if (km > 0 && km / (mins / 60) > 12) f = { type: "speed", severity: "high", title: "Unrealistic walking speed", details: `${(km / (mins / 60)).toFixed(1)} km/h over ${km.toFixed(2)} km — possible vehicle or GPS spoofing.` };
    else if (km >= 0.5 && steps < km * 300) f = { type: "steps_mismatch", severity: "medium", title: "Steps don't match distance", details: `${fmt(steps)} steps for ${km.toFixed(2)} km.` };
    else if (steps >= 3000 && km < steps / 4000) f = { type: "steps_mismatch", severity: "low", title: "Steps don't match distance", details: `${fmt(steps)} steps for only ${km.toFixed(2)} km.` };
    if (f) out.push({ id: x.id, sessionID: x.id, userID: x.userId || "", legend: x.legend || "", status: "open", createdAt: x.endTime, ...f });
  }
  return out;
}

// Detected flags merged with saved admin decisions.
async function computeFlags(sessions) {
  const [saved, sess] = await Promise.all([listSafe("flags"), sessions ? sessions : listSafe("walking_sessions")]);
  const byId = Object.fromEntries(saved.map((f) => [f.id, f]));
  const flags = detectFlags(sess).map((f) => (byId[f.id] ? { ...f, ...byId[f.id], id: f.id } : f));
  saved.forEach((f) => { if (!flags.find((x) => x.id === f.id)) flags.push(f); });
  return flags;
}

async function loadFlags() {
  $("flag-list").innerHTML = `<div class="p-5 space-y-3">${skeleton(3)}</div>`;
  state.flags = await computeFlags();
  renderFlags();
  renderNotifications();
}

function renderFlags() {
  const counts = { open: 0, escalated: 0, resolved: 0 };
  state.flags.forEach((f) => { const s = f.status || "open"; if (counts[s] !== undefined) counts[s]++; });
  document.querySelectorAll("#flag-filters .chip").forEach((c) => {
    const k = c.dataset.filter;
    c.textContent = `${k[0].toUpperCase() + k.slice(1)} (${counts[k]})`;
    c.classList.toggle("active", k === state.flagFilter);
  });
  const items = state.flags.filter((f) => (f.status || "open") === state.flagFilter)
    .sort((a, b) => (toDate(b.createdAt) || 0) - (toDate(a.createdAt) || 0));
  const sevIcon = { high: ["error", "gps_off"], medium: ["amber", "report"], low: ["slate", "info"] };
  $("flag-list").innerHTML = items.length ? items.map((f) => {
    const [c, icon] = sevIcon[f.severity] || sevIcon.medium;
    const bg = c === "error" ? "bg-error/10" : `bg-${c}-100`, fg = c === "error" ? "text-error" : `text-${c}-600`;
    const actions = f.status === "resolved"
      ? `<button data-action="flag-status" data-id="${esc(f.id)}" data-status="open" class="text-xs font-bold text-on-surface-variant bg-surface-container-low px-3 py-1.5 rounded-lg hover:bg-surface-container">Reopen</button>`
      : `<button data-action="flag-status" data-id="${esc(f.id)}" data-status="resolved" class="text-xs font-bold text-green-700 bg-green-100 px-3 py-1.5 rounded-lg hover:bg-green-200 transition-colors">Resolve</button>
         ${f.status !== "escalated" ? `<button data-action="flag-status" data-id="${esc(f.id)}" data-status="escalated" class="text-xs font-bold text-error bg-error/10 px-3 py-1.5 rounded-lg hover:bg-error/20 transition-colors">Escalate</button>` : ""}`;
    return `
    <div class="flex items-center gap-4 p-5 hover:bg-surface-container-low transition-colors">
      <div class="w-10 h-10 rounded-full ${bg} flex items-center justify-center shrink-0"><span class="material-symbols-outlined ${fg}" style="font-size:18px">${icon}</span></div>
      <div class="flex-1 min-w-0">
        <p class="font-bold text-sm">${esc(f.title)} <span class="text-[10px] uppercase font-bold text-on-surface-variant ml-1">${esc(f.severity || "")}</span></p>
        <p class="text-xs text-on-surface-variant truncate">${[f.userID && `Player ${f.userID}`, f.legend, f.sessionID && `Session ${f.sessionID}`, timeAgo(f.createdAt)].filter(Boolean).map(esc).join(" · ")}</p>
        ${f.details ? `<p class="text-xs text-on-surface-variant mt-1">${esc(f.details)}</p>` : ""}
      </div>
      <div class="flex gap-2 shrink-0">${actions}</div>
    </div>`;
  }).join("") : `<div class="p-10 text-center text-sm text-on-surface-variant">No ${state.flagFilter} flags. Suspicious walking sessions are flagged automatically.</div>`;
}

// Only fields allowed by the Firestore rules are written.
function flagDoc(f, status) {
  const d = {};
  for (const k of FLAG_FIELDS) if (f[k] !== undefined && f[k] !== null && k !== "createdAt" && k !== "updatedAt") d[k] = f[k];
  d.status = status;
  d.handledBy = admin.uid;
  d.updatedAt = serverTimestamp();
  d.createdAt = f.createdAt && typeof f.createdAt.toDate === "function" ? f.createdAt : serverTimestamp();
  return d;
}

async function setFlagStatus(id, status) {
  const f = state.flags.find((x) => x.id === id); if (!f) return;
  try {
    await setDoc(doc(db, "flags", id), flagDoc(f, status));
    f.status = status;
    renderFlags(); renderNotifications();
    showToast(status === "resolved" ? "Flag resolved" : status === "escalated" ? "Flag escalated" : "Flag reopened");
  } catch (err) { showToast(friendlyError(err), true); }
}

async function resolveAllFlags() {
  const open = state.flags.filter((f) => (f.status || "open") === "open");
  if (!open.length) return showToast("No open flags");
  if (!confirm(`Resolve all ${open.length} open flags?`)) return;
  try {
    for (let i = 0; i < open.length; i += 400) {
      const batch = writeBatch(db);
      open.slice(i, i + 400).forEach((f) => batch.set(doc(db, "flags", f.id), flagDoc(f, "resolved")));
      await batch.commit();
    }
    open.forEach((f) => (f.status = "resolved"));
    renderFlags(); renderNotifications();
    showToast(`${open.length} flags resolved`);
  } catch (err) { showToast(friendlyError(err), true); }
}

// Analytics period is shared by every KPI, chart, and export.
function analyticsStart() {
  const days = { week: 7, month: 30, year: 365 }[state.analyticsRange];
  return days ? new Date(Date.now() - days * 864e5) : null;
}
function inAnalyticsRange(value) {
  const start = analyticsStart();
  if (!start) return true;
  const d = toDate(value);
  return !!d && d >= start;
}
function selectAnalyticsRange(range) {
  if (!["week", "month", "year", "all"].includes(range)) return;
  state.analyticsRange = range;
  document.querySelectorAll("#analytics-ranges .chip").forEach((el) => el.classList.toggle("active", el.dataset.range === range));
  applyRange({ week: "7", month: "30", year: "365", all: "all" }[range]);
  loadAnalytics().catch((e) => showToast(friendlyError(e), true));
}
function renderHuntChart(completed) {
  const now = new Date();
  const buckets = [];
  const fmtKey = (d) => `${d.getFullYear()}-${d.getMonth()}-${d.getDate()}`;
  const monthKey = (d) => `${d.getFullYear()}-${d.getMonth()}`;
  const r = state.analyticsRange;
  if (r === "week") {
    for (let i = 6; i >= 0; i--) { const d = new Date(now); d.setDate(d.getDate() - i); buckets.push({ key: fmtKey(d), label: d.toLocaleDateString("en-US", { weekday: "short" }), n: 0 }); }
  } else if (r === "month") {
    for (let i = 4; i >= 0; i--) buckets.push({ key: i, label: i === 0 ? "This week" : `${i}w ago`, n: 0 });
  } else if (r === "year") {
    for (let i = 11; i >= 0; i--) { const d = new Date(now.getFullYear(), now.getMonth() - i, 1); buckets.push({ key: monthKey(d), label: d.toLocaleDateString("en-US", { month: "short" }), n: 0 }); }
  } else {
    const dates = completed.map((x) => toDate(x.endTime)).filter(Boolean);
    const first = dates.length ? Math.min(...dates.map((x) => x.getFullYear())) : now.getFullYear();
    for (let y = first; y <= now.getFullYear(); y++) buckets.push({ key: y, label: String(y), n: 0 });
  }
  completed.forEach((x) => {
    const d = toDate(x.endTime); if (!d) return;
    let key;
    if (r === "week") key = fmtKey(d);
    else if (r === "month") key = Math.floor((new Date(now.getFullYear(), now.getMonth(), now.getDate()) - new Date(d.getFullYear(), d.getMonth(), d.getDate())) / 864e5 / 7);
    else if (r === "year") key = monthKey(d);
    else key = d.getFullYear();
    const slot = buckets.find((x) => x.key === key); if (slot) slot.n++;
  });
  const maximum = Math.max(1, ...buckets.map((x) => x.n));
  $("an-monthly").innerHTML = buckets.map((x) => `<div class="flex flex-col items-center justify-end gap-1 flex-1 h-full" title="${fmt(x.n)} hunts completed"><span class="text-[10px] text-slate-400">${x.n || ""}</span><div class="w-full rounded-t bg-[#77c6fc]/70" style="height:${Math.max(3, (x.n / maximum) * 80)}%"></div><span class="text-[10px] text-slate-400 font-medium">${esc(x.label)}</span></div>`).join("");
}

// ================= ANALYTICS =================
async function loadAnalytics() {
  $("an-regions").innerHTML = skeleton(4, "h-6");
  $("an-top-quests").innerHTML = skeleton(3);
  const [sessions, leaderboard, users, userRewards, rewards] = await Promise.all([
    listSafe("walking_sessions"), listSafe("leaderboard"), listSafe("users"),
    listSafe("user_rewards"), listSafe("rewards"),
  ]);

  const scopedSessions = sessions.filter((x) => inAnalyticsRange(x.endTime || x.startTime));
  // Reward redemptions are claims issued within the chosen period.
  const redeemed = userRewards.filter((r) => isRedeemed(r) && inAnalyticsRange(r.redeemDate || r.createdAt));
  $("an-redeem-total").textContent = fmt(redeemed.length);
  $("an-redeem-30").textContent = fmt(redeemed.filter((r) => r.status === "fulfilled").length);
  const byReward = {};
  redeemed.forEach((r) => (byReward[r.rewardId] = (byReward[r.rewardId] || 0) + 1));
  const ranked = Object.entries(byReward).sort((a, b) => b[1] - a[1]).slice(0, 6);
  $("an-redeem-list").innerHTML = ranked.length ? ranked.map(([rid, n]) => {
    const rw = rewards.find((x) => x.id === rid);
    return `<div class="flex items-center gap-3 p-3 bg-surface-container-low rounded-lg">
      <div class="w-10 h-10 rounded-lg bg-amber-100 overflow-hidden flex items-center justify-center shrink-0">${rw?.imageUrl ? `<img src="${esc(rw.imageUrl)}" alt="" class="w-full h-full object-cover"/>` : `<span class="material-symbols-outlined text-amber-600" style="font-size:20px">redeem</span>`}</div>
      <div class="flex-1 min-w-0"><p class="text-sm font-bold truncate">${esc(rw?.title || rid || "Unknown reward")}</p><p class="text-xs text-on-surface-variant">${rw ? `${fmt(rewardCost(rw))} crystals` : ""}</p></div>
      <span class="font-headline font-extrabold text-lg text-primary">${fmt(n)}</span>
    </div>`;
  }).join("") : `<p class="text-sm text-on-surface-variant">Redeemed rewards from the app will appear here.</p>`;

  // Steps, distance, crystals and energy from walking sessions
  const totals = activityTotals(scopedSessions);
  const { steps, km, crystals, energy, completed } = totals;
  $("an-steps").textContent = compact(steps);
  $("an-steps-sub").textContent = `${km.toFixed(1)} km across ${fmt(completed.length)} completed hunts`;
  $("an-crystals").textContent = compact(crystals);
  $("an-crystals-sub").textContent = `${compact(energy)} energy generated`;

  // Average session duration
  const durations = completed.map((x) => { const a = toDate(x.startTime), b = toDate(x.endTime); return a && b ? (b - a) / 60000 : 0; }).filter((d) => d > 0);
  $("an-duration").textContent = durations.length ? `${Math.round(durations.reduce((a, b) => a + b, 0) / durations.length)} min` : "–";
  $("an-duration-sub").textContent = `${fmt(durations.length)} completed hunts`;

  // Sessions by legend
  const byLegend = {};
  completed.forEach((x) => { const l = (x.legend || "").trim() || "Free walk"; byLegend[l] = (byLegend[l] || 0) + 1; });
  const legends = Object.entries(byLegend).sort((a, b) => b[1] - a[1]);
  $("an-top").textContent = legends[0]?.[0] || "–";
  $("an-top-sub").textContent = legends[0] ? `${fmt(legends[0][1])} completed hunts` : "No hunts completed yet";
  $("an-regions").innerHTML = legends.length ? legends.slice(0, 6).map(([name, n], i) => {
    const pct = Math.round((n / completed.length) * 100);
    return `<div class="flex items-center gap-4">
      <span class="text-sm font-bold text-on-surface w-32 truncate" title="${esc(name)}">${esc(name)}</span>
      <div class="flex-1 h-3 bg-surface-container-low rounded-full overflow-hidden"><div class="h-full ${i < 2 ? "bg-primary" : "bg-[#77c6fc]"} rounded-full" style="width:${pct}%"></div></div>
      <span class="text-sm font-bold text-on-surface w-10 text-right">${pct}%</span>
    </div>`;
  }).join("") : `<p class="text-sm text-on-surface-variant">Walking sessions from the app will appear here.</p>`;

  // Top walkers — leaderboard collection, falling back to player profiles
  let board;
  if (state.analyticsRange === "all") board = (leaderboard.length ? leaderboard : users).slice();
  else {
    const playerSteps = {};
    scopedSessions.forEach((x) => { if (x.userId) playerSteps[x.userId] = (playerSteps[x.userId] || 0) + num(x.numSteps); });
    board = Object.entries(playerSteps).map(([id, totalSteps]) => ({ id, totalSteps, name: users.find((x) => x.id === id)?.name || "Player", crystals: 0, streak: 0 }));
  }
  board = board.sort((a, b) => num(b.totalSteps) - num(a.totalSteps)).slice(0, 5);
  const badge = ["bg-primary", "bg-[#77c6fc]", "bg-tertiary", "bg-secondary", "bg-outline"];
  $("an-top-quests").innerHTML = board.length ? board.map((u, i) => `
    <div class="flex items-center gap-3 p-3 bg-surface-container-low rounded-lg">
      <div class="w-7 h-7 rounded-full ${badge[i]} text-white flex items-center justify-center text-xs font-black">${i + 1}</div>
      <div class="flex-1 min-w-0"><p class="text-sm font-bold truncate">${esc(u.name || "Player")}</p><p class="text-xs text-on-surface-variant">${fmt(u.totalSteps)} steps · ${fmt(u.crystals)} crystals</p></div>
      <span class="text-xs font-bold text-amber-600 whitespace-nowrap">🔥 ${fmt(u.streak)}</span>
    </div>`).join("") : `<p class="text-sm text-on-surface-variant">The leaderboard from the app will appear here.</p>`;

  renderHuntChart(completed);
}

// ================= EXPORT =================
const DATE_FIELD = { walking_sessions: "startTime", user_rewards: "createdAt" };
const PII = ["name", "email", "contactInfo", "username"];


function applyRange(range) {
  state.exportRange = range;
  document.querySelectorAll("#ex-ranges .chip").forEach((c) => c.classList.toggle("active", c.dataset.range === range));
  const today = new Date();
  const iso = (d) => d.toISOString().slice(0, 10);
  if (range === "all") { $("ex-start").value = ""; $("ex-end").value = ""; }
  else {
    const start = range === "ytd" ? new Date(today.getFullYear(), 0, 1) : new Date(Date.now() - (Number(range) - 1) * 864e5);
    $("ex-start").value = iso(start); $("ex-end").value = iso(today);
  }
  refreshExportEstimate();
}

function filteredExportRows(rows, dataset) {
  const start = $("ex-start").value ? new Date($("ex-start").value + "T00:00:00") : null;
  const end = $("ex-end").value ? new Date($("ex-end").value + "T23:59:59") : null;
  const field = DATE_FIELD[dataset] || "createdAt";
  return rows.filter((r) => {
    const d = toDate(r[field]);
    if (!d) return !start && !end;
    return (!start || d >= start) && (!end || d <= end);
  });
}

async function loadExportRows(dataset) {
  if (dataset !== "analytics_summary") return list(dataset);
  const [sessions, claims] = await Promise.all([listSafe("walking_sessions"), listSafe("user_rewards")]);
  const dated = sessions.filter((x) => inAnalyticsRange(x.endTime || x.startTime));
  const totals = activityTotals(dated);
  const claimsCount = claims.filter((x) => isRedeemed(x) && inAnalyticsRange(x.redeemDate || x.createdAt)).length;
  const durations = totals.completed.map((x) => (toDate(x.endTime) - toDate(x.startTime)) / 60000).filter((x) => Number.isFinite(x) && x > 0);
  return [{ period: state.analyticsRange, steps: totals.steps, distanceKm: Number(totals.km.toFixed(2)), crystalsEarned: totals.crystals,
    completedHunts: totals.completed.length, avgSessionMinutes: durations.length ? Math.round(durations.reduce((a,b) => a+b, 0)/durations.length) : 0,
    rewardClaims: claimsCount, generatedAt: new Date().toISOString() }];
}
let exportCache = { dataset: null, rows: [] };
async function refreshExportEstimate() {
  const dataset = $("ex-dataset").value;
  $("ex-count").textContent = "…";
  try {
    if (exportCache.dataset !== dataset || dataset === "analytics_summary") exportCache = { dataset, rows: await loadExportRows(dataset) };
    const rows = dataset === "analytics_summary" ? exportCache.rows : filteredExportRows(exportCache.rows, dataset);
    $("ex-count").textContent = fmt(rows.length);
    const bytes = new Blob([JSON.stringify(rows)]).size;
    $("ex-size").textContent = bytes < 1024 ? `${bytes} B` : bytes < 1048576 ? `${(bytes / 1024).toFixed(1)} KB` : `${(bytes / 1048576).toFixed(1)} MB`;
  } catch (e) {
    $("ex-count").textContent = "–"; $("ex-size").textContent = "–";
    showToast(friendlyError(e), true);
  }
}

function pseudonym(v) {
  let h = 0; for (const ch of String(v)) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  return `anon-${h.toString(36)}`;
}
function serialize(v) {
  if (v == null) return "";
  if (typeof v.toDate === "function") return v.toDate().toISOString();
  if (typeof v === "object") return JSON.stringify(v);
  return v;
}

async function doExport() {
  const btn = $("ex-btn");
  setBusy(btn, true, "Generating…");
  try {
    const dataset = $("ex-dataset").value;
    exportCache = { dataset, rows: await loadExportRows(dataset) };
    const anon = isToggled($("toggle-anon"));
    const source = dataset === "analytics_summary" ? exportCache.rows : filteredExportRows(exportCache.rows, dataset);
    const rows = source.map((r) => {
      const out = {};
      for (const [k, v] of Object.entries(r)) {
        if (anon && PII.includes(k)) continue;
        if (anon && (k === "userId" || (dataset === "users" && k === "id"))) { out[k] = pseudonym(v); continue; }
        out[k] = serialize(v);
      }
      return out;
    });
    if (!rows.length) return showToast("No records match this range.", true);

    const format = document.querySelector('input[name="ex-format"]:checked').value;
    let content, type;
    if (format === "json") { content = JSON.stringify(rows, null, 2); type = "application/json"; }
    else {
      const cols = [...new Set(rows.flatMap((r) => Object.keys(r)))];
      const cell = (v) => { const s = String(v ?? ""); return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s; };
      content = [cols.join(","), ...rows.map((r) => cols.map((c) => cell(r[c])).join(","))].join("\n");
      type = "text/csv";
    }
    const a = document.createElement("a");
    a.href = URL.createObjectURL(new Blob([content], { type }));
    a.download = `masar-${dataset}-${new Date().toISOString().slice(0, 10)}.${format}`;
    a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 1000);
    showToast(`Exported ${fmt(rows.length)} records`);
  } catch (e) {
    showToast(friendlyError(e), true);
  } finally { setBusy(btn, false); }
}

// ================= SETTINGS =================
async function loadSettings() {
  $("set-username").value = admin.username || "";
  $("set-email").value = admin.email || "";
  setToggle($("toggle-email"), admin.preferences?.emailNotifications ?? true);
}

async function saveSettings(e) {
  e.preventDefault();
  const username = $("set-username").value.trim();
  if (!username) return showToast("Username can't be empty.", true);
  const preferences = { emailNotifications: isToggled($("toggle-email")) };
  const btn = $("settings-save");
  setBusy(btn, true);
  try {
    await updateDoc(doc(db, "admins", admin.uid), { username, preferences });
    await updateProfile(auth.currentUser, { displayName: username }).catch(() => {});
    Object.assign(admin, { username, preferences });
    renderProfile();
    showToast("Settings saved successfully!");
  } catch (err) { showToast(friendlyError(err), true); }
  finally { setBusy(btn, false); }
}

async function resetPassword() {
  try { await sendPasswordResetEmail(auth, admin.email); showToast(`Reset link sent to ${admin.email}`); }
  catch (err) { showToast(friendlyError(err), true); }
}

// ================= SAMPLE DATA =================
async function seedSampleData() {
  if (!confirm("Add sample rewards and partners to your Firestore database?")) return;
  try {
    const batch = writeBatch(db);
    const now = serverTimestamp();
    const add = (col, data) => { const ref = doc(collection(db, col)); batch.set(ref, { ...data, createdAt: now, updatedAt: now }); return ref.id; };
    const y = new Date().getFullYear();
    const pAlRai = add("partners", { businessName: "AlRai Sports", description: "Sportswear retailer", contactInfo: "partners@alrai.example", tier: "Platinum", renewalDate: `${y}-12-15`, verified: true });
    add("partners", { businessName: "Kuwait Wellness Co.", description: "Gyms and wellness centres", contactInfo: "hello@kwellness.example", tier: "Gold", renewalDate: `${y + 1}-06-01`, verified: true });
    add("partners", { businessName: "TechMove Gulf", description: "Wearables and smart footwear", contactInfo: "biz@techmove.example", tier: "Silver", renewalDate: `${y + 1}-03-01`, verified: false });
    add("rewards", { title: "Gold Tier Badge", description: "Awarded to the top 1% of hunters each month.", conditions: "Finish in the top 1% of monthly completions.", crystalCost: 5000, availStatus: "Active", expDate: null, partnerId: "", adminId: admin.uid });
    add("rewards", { title: "Coffee Voucher", description: "Redeemable at 50+ partner locations in Kuwait.", conditions: "Reach level 3. One voucher per user per month.", crystalCost: 1000, availStatus: "Active", expDate: `${y + 1}-01-31`, partnerId: "", adminId: admin.uid });
    add("rewards", { title: "Sports Store Discount", description: "15% off at AlRai Sports.", conditions: "Complete any 5 Legend Hunts.", crystalCost: 2500, availStatus: "Draft", expDate: null, partnerId: pAlRai, adminId: admin.uid });
    await batch.commit();
    showToast("Sample data added");
    loadDashboard();
  } catch (err) { showToast(friendlyError(err), true); }
}

// ================= HUNTS =================
const huntMonsters = { bu_daryah: "Bu Daryah", om_el_saaf: "Om El-Saaf", tantal: "Tantal", hamaret_al_qayla: "Hamaret Al Qayla" };
const achievementMetrics = {
  hunt: "Complete any hunt",
  monster: "Capture a specific monster",
  hunt_steps: "Steps in one hunt",
  hunt_crystals: "Crystals earned in one hunt",
  total_steps: "Total lifetime steps",
  active_day_streak: "Consecutive hunt days",
};
const achievementCategories = { walking: 'Walking', monster_hunting: 'Monster Hunting', crystals_energy: 'Crystals and Energy', consistency: 'Consistency', special: 'Special Challenges' };
const achievementCategory = (metric) => ({ hunt: 'monster_hunting', monster: 'monster_hunting', hunt_steps: 'walking', hunt_crystals: 'crystals_energy', total_steps: 'walking', active_day_streak: 'consistency' })[metric];
const categoryImages = { walking: 'walking', monster_hunting: 'monster_hunting', crystals_energy: 'crystals_energy', consistency: 'consistency_streaks', special: 'special_challenges' };
const categoryImage = (category) => categoryImages[category] ? `assets/badges/${categoryImages[category]}.png` : '';
const defaultSpawn = {
  name: "Sabah Al Salem Walkway",
  mapsUrl: "https://www.google.com/maps/search/?api=1&query=29.2558375%2C48.078453125",
  latitude: 29.2558375,
  longitude: 48.078453125,
  arrivalRadiusMeters: 30,
};
let huntSites = {};
let huntAchievements = [];
const spawnCollection = (monsterId) => collection(db, "monsters", monsterId, "spawn_locations");
const huntButton = "text-sm font-bold text-primary hover:underline";

async function loadHunts() {
  $("hunt-monsters").innerHTML = skeleton(2);
  $("hunt-achievements").innerHTML = skeleton(2);
  try {
    const locations = await Promise.all(Object.entries(huntMonsters).map(async ([id, name]) => {
      const monster = await getDoc(doc(db, "monsters", id));
      const managed = monster.data()?.spawnCatalogEnabled === true;
      const sites = managed ? (await getDocs(spawnCollection(id))).docs.map((d) => ({ ...d.data(), id: d.id })) :
        (["tantal", "hamaret_al_qayla"].includes(id) ? [] : [{ ...defaultSpawn, id: "default" }]);
      return [id, { name, sites }];
    }));
    huntSites = Object.fromEntries(locations);
    huntAchievements = (await list("achievements"))
      .sort((a, b) => String(a.name || '').localeCompare(String(b.name || '')));
    renderHunts();
  } catch (err) {
    const message = emptyState("error", err?.code === "permission-denied" ?
      "Firebase denied access to Hunts. Check the project's Hunts rules." : friendlyError(err));
    $("hunt-monsters").innerHTML = message;
    $("hunt-achievements").innerHTML = message;
  }
}

function safeMapsLink(value) {
  try { return mapsUrl(value).url.href; }
  catch { return ""; }
}

function renderHunts() {
  $("hunt-monsters").innerHTML = `<p class="text-sm text-on-surface-variant mb-3">Choose the maximum estimated walking time for each location. Players must reach the location before the hunt starts. Times are estimates based on proximity, not mapped walking routes.</p>` + Object.entries(huntSites).map(([id, { name, sites }]) => `
    <div class="rounded-lg bg-surface-container-low p-4">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <h4 class="font-bold">${esc(name)}</h4>
        <button class="${huntButton}" data-action="new-spawn" data-id="${id}">Add Location</button>
      </div>
      ${sites.length ? sites.map((site) => {
        const link = safeMapsLink(site.mapsUrl);
        return `<div class="flex flex-wrap items-center justify-between gap-2 border-t border-outline-variant/20 mt-3 pt-3">
          <div class="min-w-0">
            ${link ? `<a class="${huntButton} break-words" href="${esc(link)}" target="_blank" rel="noopener noreferrer">${esc(site.name)}</a>` :
              `<p class="text-sm font-bold">${esc(site.name)}</p>`}
            <p class="text-xs text-on-surface-variant">${Number(site.latitude).toFixed(5)}, ${Number(site.longitude).toFixed(5)} · Up to ${esc(site.walkingMinutes ?? 60)} minutes walking</p>
          </div>
          <button class="${huntButton}" data-action="edit-spawn" data-id="${esc(site.id)}" data-monster="${id}">Manage</button>
        </div>`;
      }).join("") : '<p class="mt-3 text-sm text-on-surface-variant">No spawn locations.</p>'}
    </div>`).join("");
  const search = ($('achievement-search')?.value || '').trim().toLowerCase();
  const category = $('achievement-category')?.value || '';
  const filtered = huntAchievements.filter((badge) => (!category || (badge.category || achievementCategory(badge.metric)) === category) && `${badge.name || ''} ${badge.description || ''}`.toLowerCase().includes(search));
  $("hunt-achievements").innerHTML = filtered.length ? filtered.map((badge) => {
    const condition = badge.metric === "monster" ? `Capture ${huntMonsters[badge.monsterId]}` :
      badge.metric === "hunt" ? achievementMetrics.hunt : achievementMetrics[badge.metric] ? `${fmt(badge.target)}+ ${achievementMetrics[badge.metric].toLowerCase()}` : 'Unsupported condition — edit after data tracking is available';
    return `<div class="rounded-lg bg-surface-container-low p-4">
      <div class="flex items-start justify-between gap-3">
        <div>${categoryImage(badge.category || achievementCategory(badge.metric)) ? `<img src="${esc(categoryImage(badge.category || achievementCategory(badge.metric)))}" alt="" class="w-12 h-12 rounded-lg object-contain mb-2"/>` : ''}<h4 class="font-bold break-words">${esc(badge.name)}</h4><p class="text-sm text-on-surface-variant">${esc(badge.description)}</p></div>
        ${Object.hasOwn(achievementMetrics, badge.metric) ? `<button class="${huntButton}" data-action="edit-achievement" data-id="${esc(badge.id)}">Edit</button>` : ''}
      </div>
      <p class="mt-2 text-xs font-semibold">${esc(achievementCategories[badge.category || achievementCategory(badge.metric)] || 'Unsupported')} · ${esc(condition)} · ${badge.active ? "Active" : "Inactive"}</p>
    </div>`;
  }).join("") : emptyState("military_tech", "No achievements yet.");
}

function spawnModal(monsterId, site) {
  if (!Object.hasOwn(huntMonsters, monsterId)) return;
  const monsterRef = doc(db, "monsters", monsterId);
  const siteRef = site ? doc(spawnCollection(monsterId), site.id) : doc(spawnCollection(monsterId));
  async function saveSite(values) {
    await runTransaction(db, async (tx) => {
      const monster = await tx.get(monsterRef);
      if (monster.data()?.spawnCatalogEnabled !== true) {
        tx.set(monsterRef, { name: huntMonsters[monsterId], spawnCatalogEnabled: true }, { merge: true });
        if (!["tantal", "hamaret_al_qayla"].includes(monsterId) && siteRef.id !== "default") tx.set(doc(spawnCollection(monsterId), "default"), defaultSpawn);
      }
      if (values) tx.set(siteRef, values);
      else tx.delete(siteRef);
    });
    await loadHunts();
  }
  openModal({
    title: `${site ? "Edit" : "Add"} location · ${huntMonsters[monsterId]}`,
    fields: [
      { name: "name", label: "Location name", value: site?.name, required: true },
      { name: "mapsUrl", label: "Google Maps link", type: "url", value: site?.mapsUrl, required: true },
      { name: "latitude", label: "Latitude (optional if the link contains a pin)", value: site?.latitude },
      { name: "longitude", label: "Longitude (optional if the link contains a pin)", value: site?.longitude },
      { name: "walkingMinutes", label: "Maximum estimated walking time", type: "select", options: [15, 30, 45, 60].map((minutes) => [minutes, `${minutes} minutes`]), value: site?.walkingMinutes ?? 60, required: true },
    ],
    onSave: async ({ name, mapsUrl: link, latitude, longitude, walkingMinutes }) => {
      const parsed = mapsUrl(link);
      if (name.length > 120) {
        throw new Error("Use a location name up to 120 characters.");
      }
      let pin;
      if (latitude || longitude) {
        pin = { latitude: Number(latitude), longitude: Number(longitude) };
        if (!latitude || !longitude || !Number.isFinite(pin.latitude) || !Number.isFinite(pin.longitude) ||
          Math.abs(pin.latitude) > 90 || Math.abs(pin.longitude) > 180) throw new Error("Enter a valid latitude and longitude.");
      } else {
        pin = mapCoordinates(parsed.url);
        if (!pin && parsed.short) {
          const response = await fetch("/api/resolve-hunt-map", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ url: parsed.url.href }),
          });
          if (response.ok) pin = await response.json();
        }
        if (!pin) throw new Error("Couldn't find the pin. Enter its latitude and longitude from Google Maps.");
      }
      const response = await fetch("/api/hunt-walking-range", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ walkingMinutes: Number(walkingMinutes) }),
      });
      const range = await response.json();
      if (!response.ok) throw new Error(range.error || "Could not calculate the walking range. Please try again.");
      await saveSite({ name, mapsUrl: parsed.url.href, ...pin,
        walkingMinutes: range.walkingMinutes, maxSearchMeters: range.maxSearchMeters,
        arrivalRadiusMeters: site?.arrivalRadiusMeters ?? 30 });
      showToast("Spawn location saved");
    },
    onDelete: site ? async () => {
      if (!window.confirm(`Remove ${site.name} from ${huntMonsters[monsterId]}'s spawn locations?`)) return;
      try { await saveSite(null); closeModal(); showToast("Spawn location removed"); }
      catch (err) { showToast(friendlyError(err), true); }
    } : null,
  });
  const fields = $("modal-form").elements;
  fields.mapsUrl.addEventListener("input", () => {
    fields.latitude.value = "";
    fields.longitude.value = "";
  });
}

function achievementModal(badge) {
  openModal({
    title: badge ? "Edit achievement" : "Add achievement",
    fields: [
      { name: "name", label: "Badge name", value: badge?.name, required: true },
      { name: "description", label: "Description", type: "textarea", value: badge?.description, required: true },
      { name: "category", label: "Category", type: "select", options: Object.entries(achievementCategories), value: badge?.category || achievementCategory(badge?.metric) || 'walking' },
      { name: "metric", label: "Unlock condition", type: "select", options: Object.entries(achievementMetrics), value: badge?.metric ?? "hunt" },
      { name: "monsterId", label: "Monster", type: "select", options: Object.entries(huntMonsters), value: badge?.monsterId || "bu_daryah" },
      { name: "target", label: "Minimum count", type: "number", value: badge?.target ?? 1 },
      { name: "active", label: "Players can earn this badge", type: "checkbox", value: badge?.active ?? true },
    ],
    onSave: async ({ name, description, category, metric, monsterId, target, active }) => {
      if (metric === "hunt" || metric === "monster") target = 1;
      if (name.length > 80 || description.length > 500 || !Number.isInteger(target) || target < 1 || target > 1000000) {
        throw new Error("Use a name up to 80 characters, a description up to 500 characters, and a whole-number count from 1 to 1,000,000.");
      }
      if (achievementCategory(metric) !== category) throw new Error('Choose a condition for the selected category.');
      const data = { name, description, category, metric, target, active, monsterId: metric === "monster" ? monsterId : "" };
      if (badge) await updateDoc(doc(db, "achievements", badge.id), data);
      else await addDoc(collection(db, "achievements"), data);
      await loadHunts();
      showToast("Achievement saved");
    },
  });
  const fields = $("modal-form").elements;
  const badgePreview = document.createElement('img');
  badgePreview.className = 'w-20 h-20 object-contain';
  badgePreview.alt = 'Category badge preview';
  $('modal-fields').appendChild(badgePreview);
  const condition = document.createElement('p');
  condition.className = 'text-sm font-semibold text-on-surface-variant';
  $('modal-fields').appendChild(condition);
  const refresh = () => {
    badgePreview.src = categoryImage(fields.category.value);
    for (const option of fields.metric.options) option.hidden = achievementCategory(option.value) !== fields.category.value;
    if (achievementCategory(fields.metric.value) !== fields.category.value) {
      const first = Array.from(fields.metric.options).find((option) => !option.hidden);
      if (first) fields.metric.value = first.value;
    }
    const available = achievementCategory(fields.metric.value) === fields.category.value;
    fields.metric.parentElement.classList.toggle('hidden', !available);
    fields.monsterId.parentElement.classList.toggle("hidden", fields.metric.value !== "monster");
    fields.target.parentElement.classList.toggle("hidden", !available || !["hunt_steps", "hunt_crystals", "total_steps", "active_day_streak"].includes(fields.metric.value));
    condition.textContent = available ? `Preview: ${fields.name.value || 'Badge'} — ${achievementMetrics[fields.metric.value]}${fields.metric.value === 'monster' ? `: ${huntMonsters[fields.monsterId.value]}` : fields.target.parentElement.classList.contains('hidden') ? '' : `: ${fields.target.value}`}` : 'This category needs additional event tracking before its conditions can award badges.';
  };
  fields.metric.addEventListener("change", refresh);
  fields.category.addEventListener("change", refresh);
  fields.monsterId.addEventListener("change", refresh);
  fields.target.addEventListener("input", refresh);
  fields.name.addEventListener("input", refresh);
  refresh();
}

// ================= EVENTS =================
const loaders = {
  dashboard: loadDashboard, rewards: loadRewards,
  sponsors: loadSponsors, flags: loadFlags, hunts: loadHunts, analytics: async () => { await loadAnalytics(); await refreshExportEstimate(); }, settings: loadSettings,
  "sp-dashboard": loadSponsorDashboard, "sp-products": loadSponsorProducts,
  "sp-rewards": loadSponsorRewards, "sp-verify": async () => {},
  "sp-profile": loadSponsorProfile,
};

function bindEvents() {
  document.addEventListener("click", (e) => {
    const navEl = e.target.closest("[data-nav], .nav-link[data-page], .nav-top[data-page]");
    if (navEl) { navigate(navEl.dataset.nav || navEl.dataset.page, navEl.dataset.id); return; }

    const toggle = e.target.closest(".toggle");
    if (toggle) { toggle.classList.toggle("toggle-on"); return; }

    const fchip = e.target.closest("#flag-filters .chip");
    if (fchip) { state.flagFilter = fchip.dataset.filter; renderFlags(); return; }
    const achip = e.target.closest("#analytics-ranges .chip");
    if (achip) { selectAnalyticsRange(achip.dataset.range); return; }
    const rchip = e.target.closest("#ex-ranges .chip");
    if (rchip) { applyRange(rchip.dataset.range); return; }

    const act = e.target.closest("[data-action]");
    if (act) {
      const { action, id } = act.dataset;
      switch (action) {
        case "toggle-sidebar": return toggleSidebar();
        case "close-sidebar": return closeSidebar();
        case "dropdown": {
          const menu = $(act.dataset.target); const open = menu.classList.contains("open");
          document.querySelectorAll(".dropdown-menu").forEach((d) => d.classList.remove("open"));
          if (!open) menu.classList.add("open");
          return;
        }
        case "toggle-password": {
          const inp = $("login-password"); const show = inp.type === "password";
          inp.type = show ? "text" : "password";
          act.querySelector("span").textContent = show ? "visibility_off" : "visibility";
          return;
        }
        case "forgot": return forgotPassword();
        case "new-product": return sponsorProductModal();
        case "edit-product": return sponsorProductModal(sponsorProducts.find((p) => p.id === id));
        case "select-product": return adminSelectProduct(id);
        case "approve-sponsor": return adminReviewApplication(id, "approved");
        case "reject-sponsor": return adminReviewApplication(id, "rejected");
        case "toggle-partner": return toggleSponsorAccount(id).catch((e) => showToast(friendlyError(e), true));
        case "deny-product": return adminDenyProduct(id).catch((e) => showToast(friendlyError(e), true));
        case "admin-edit-product": return adminEditProduct(id);
        case "generate-description": return generateProductDescription();
        case "logout": return logout();
        case "seed": return seedSampleData();
        case "new-reward": return rewardModal();
        case "edit-reward": return rewardModal(state.rewards.find((r) => r.id === id));
        case "new-partner": return partnerModal();
        case "edit-partner": return partnerModal(state.partners.find((p) => p.id === id));
        case "flag-status": return setFlagStatus(id, act.dataset.status);
        case "resolve-all": return resolveAllFlags();
        case "new-spawn": return spawnModal(id);
        case "edit-spawn": {
          const monsterId = act.dataset.monster;
          const site = huntSites[monsterId]?.sites.find((entry) => entry.id === id);
          if (site) return spawnModal(monsterId, site);
          return;
        }
        case "new-achievement": return achievementModal();
        case "edit-achievement": return achievementModal(huntAchievements.find((badge) => badge.id === id));
        case "close-modal": return closeModal();
        case "modal-img-remove": {
          const img = modalImages[act.dataset.field];
          if (img) { if (img.previewUrl) URL.revokeObjectURL(img.previewUrl); Object.assign(img, { file: null, previewUrl: "", removed: true }); renderImagePreview(act.dataset.field); }
          return;
        }
        case "export": return doExport();
        case "reset-password": return resetPassword();
      }
    }

    if (!e.target.closest(".relative")) document.querySelectorAll(".dropdown-menu").forEach((d) => d.classList.remove("open"));
  });

  $("login-form").addEventListener("submit", doLogin);
  $("signup-form").addEventListener("submit", doSponsorSignup);
  $("signup-form").elements.logo.addEventListener("change", (event) => {
    const preview = $("signup-logo-preview");
    if (preview.dataset.objectUrl) URL.revokeObjectURL(preview.dataset.objectUrl);
    const file = event.target.files[0];
    preview.classList.toggle("hidden", !file);
    if (file) { preview.dataset.objectUrl = URL.createObjectURL(file); preview.src = preview.dataset.objectUrl; }
    else { preview.removeAttribute("src"); delete preview.dataset.objectUrl; }
  });
  $("settings-form").addEventListener("submit", saveSettings);
  $('achievement-search').addEventListener('input', renderHunts);
  $('achievement-category').addEventListener('change', renderHunts);
  $("modal-form").addEventListener("submit", submitModal);
  $("claim-verify-form").addEventListener("submit", verifyClaim);
  $("modal-form").addEventListener("change", (e) => { if (e.target.matches("[data-image-input]")) onModalImageChosen(e.target); });
  $("modal-delete").addEventListener("click", () => modalCfg?.onDelete?.());
  $("modal").addEventListener("click", (e) => { if (e.target.id === "modal") closeModal(); });
  document.addEventListener("keydown", (e) => { if (e.key === "Escape") closeModal(); });
  $("ex-dataset").addEventListener("change", refreshExportEstimate);
  ["ex-start", "ex-end"].forEach((id) => $(id).addEventListener("change", () => {
    document.querySelectorAll("#ex-ranges .chip").forEach((c) => c.classList.remove("active"));
    refreshExportEstimate();
  }));
}

// ================= BOOT =================
function bootError(msg) {
  $("boot-spinner").style.display = "none";
  $("boot-msg").innerHTML = msg;
}

async function boot() {
  document.querySelectorAll(".year").forEach((el) => (el.textContent = new Date().getFullYear()));
  bindEvents();

  let cfg;
  try {
    cfg = firebaseConfig;
    if (!cfg.apiKey || cfg.apiKey.startsWith("REPLACE_")) throw new Error("Open public/js/firebase-config.js and enter your Firebase web app configuration.");
  } catch (e) {
    console.error(e);
    return bootError(`Couldn't load the Firebase configuration.<br><span class="text-xs opacity-70">${esc(e.message)}</span>`);
  }

  const fbApp = initializeApp(cfg);
  auth = getAuth(fbApp);
  db = getFirestore(fbApp);

  onAuthStateChanged(auth, async (user) => {
    if (registeringSponsor) return;
    if (!user) {
      admin = null; accountRole = null; sponsorPartnerId = null;
      $("boot").classList.add("hidden");
      navigate("login");
      return;
    }
    try { await resolveAccount(user); }
    catch (e) { console.error(e); await signOut(auth); showLoginMessage("error", friendlyError(e)); }

  });
}

boot();
