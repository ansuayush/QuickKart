const API_CANDIDATES = [
  "/api",
  "http://localhost:5082/api",
  "http://localhost:5081/api",
  "http://localhost:5080/api",
  "http://localhost:53183/api"
];

let API_URL = "/api";

function token() {
  return localStorage.getItem("qk_token");
}

export function getUser() {
  const raw = localStorage.getItem("qk_user");
  return raw ? JSON.parse(raw) : null;
}

export function setSession(data) {
  if (!data?.token) throw new Error("Login failed.");
  localStorage.setItem("qk_token", data.token);
  localStorage.setItem("qk_user", JSON.stringify({ userId: data.userId, name: data.name, email: data.email, role: data.role }));
}

export function clearSession() {
  localStorage.removeItem("qk_token");
  localStorage.removeItem("qk_user");
}

export function localWishIds() {
  try { return JSON.parse(localStorage.getItem("qk_wish") || "[]"); } catch { return []; }
}

export async function toggleWish(productId, wished) {
  if (getUser()) {
    try {
      const items = wished ? await api.removeWish(productId) : await api.addWish(productId);
      const ids = items.map(i => i.productId);
      localStorage.setItem("qk_wish", JSON.stringify(ids));
      window.dispatchEvent(new Event("qk-wish"));
      return ids;
    } catch { /* older API */ }
  }
  const ids = localWishIds();
  const on = wished ?? ids.includes(productId);
  const next = on ? ids.filter(id => id !== productId) : [...ids, productId];
  localStorage.setItem("qk_wish", JSON.stringify(next));
  window.dispatchEvent(new Event("qk-wish"));
  return next;
}

async function request(path, options = {}) {
  const headers = { "Content-Type": "application/json", ...(options.headers || {}) };
  if (token()) headers.Authorization = `Bearer ${token()}`;
  let lastError = new Error("Unable to reach the API. Start GroceryDelivery.Api and refresh.");
  const bases = [API_URL, ...API_CANDIDATES.filter(u => u !== API_URL)];
  for (const base of bases) {
    try {
      const r = await fetch(`${base}${path}`, { ...options, headers });
      if (r.status === 204) {
        API_URL = base;
        return null;
      }
      const text = await r.text();
      let body = null;
      try { body = text ? JSON.parse(text) : null; } catch { body = text; }
      if (!r.ok) {
        const message = typeof body === "string" ? body : body?.title || body?.message || `Request failed (${r.status})`;
        if (r.status === 404 || r.status >= 500) {
          lastError = new Error(message);
          continue;
        }
        throw new Error(message);
      }
      API_URL = base;
      return body;
    } catch (e) {
      if (e instanceof TypeError) {
        lastError = new Error("Unable to reach the API. Start GroceryDelivery.Api and refresh.");
        continue;
      }
      throw e;
    }
  }
  throw lastError;
}

export const api = {
  products: (search = "", categoryId) => {
    const q = new URLSearchParams();
    if (search) q.set("search", search);
    if (categoryId) q.set("categoryId", categoryId);
    const qs = q.toString();
    return request(`/products${qs ? `?${qs}` : ""}`);
  },
  product: (id) => request(`/products/${id}`),
  categories: () => request("/categories"),
  register: (payload) => request("/auth/register", { method: "POST", body: JSON.stringify(payload) }),
  login: (payload) => request("/auth/login", { method: "POST", body: JSON.stringify(payload) }),
  cart: () => request("/cart"),
  addToCart: (productId, quantity = 1) => request("/cart", { method: "POST", body: JSON.stringify({ productId, quantity }) }),
  updateCart: (productId, quantity) => request(`/cart/${productId}`, { method: "PUT", body: JSON.stringify({ productId, quantity }) }),
  addresses: () => request("/addresses"),
  addAddress: (payload) => request("/addresses", { method: "POST", body: JSON.stringify(payload) }),
  orders: () => request("/orders"),
  order: (id) => request(`/orders/${id}`),
  checkout: (payload) => request("/orders/checkout", { method: "POST", body: JSON.stringify(payload) }),
  charge: (payload) => request("/payments/charge", { method: "POST", body: JSON.stringify(payload) }),
  tracking: (id) => request(`/orders/${id}/tracking`),
  deliveryBoys: () => request("/deliveryboys"),
  assignRider: (id, deliveryBoyId) => request(`/orders/${id}/assign`, { method: "PUT", body: JSON.stringify({ deliveryBoyId }) }),
  availableRiders: (id) => request(`/orders/${id}/available-riders`),
  riderJobs: () => request("/orders/rider-jobs"),
  riderAccept: (id) => request(`/orders/${id}/accept`, { method: "POST" }),
  riderReject: (id) => request(`/orders/${id}/reject`, { method: "POST" }),
  riderArrive: (id) => request(`/orders/${id}/arrive-store`, { method: "POST" }),
  riderPickup: (id) => request(`/orders/${id}/pickup`, { method: "POST" }),
  riderStart: (id) => request(`/orders/${id}/start-delivery`, { method: "POST" }),
  riderComplete: (id) => request(`/orders/${id}/complete`, { method: "POST" }),
  rateOrder: (id, stars, review, preferSameRider = false) => request(`/orders/${id}/rating`, { method: "POST", body: JSON.stringify({ stars, review, preferSameRider }) }),
  preferRider: (id) => request(`/orders/${id}/prefer-rider`, { method: "POST" }),
  preferredRider: () => request("/deliveryboys/preferred"),
  wishlist: () => request("/wishlist"),
  addWish: (productId) => request(`/wishlist/${productId}`, { method: "POST" }),
  removeWish: (productId) => request(`/wishlist/${productId}`, { method: "DELETE" }),
  updateOrderStatus: (id, status) => request(`/orders/${id}/status`, { method: "PUT", body: JSON.stringify({ status }) }),
  saveProduct: (payload, id) => request(id ? `/products/${id}` : "/products", { method: id ? "PUT" : "POST", body: JSON.stringify(payload) }),
  deleteProduct: (id) => request(`/products/${id}`, { method: "DELETE" }),
  saveRider: (payload, id) => request(id ? `/deliveryboys/${id}` : "/deliveryboys", { method: id ? "PUT" : "POST", body: JSON.stringify(payload) }),
  deleteRider: (id) => request(`/deliveryboys/${id}`, { method: "DELETE" }),
  admins: () => request("/admins"),
  saveAdmin: (payload, id) => request(id ? `/admins/${id}` : "/admins", { method: id ? "PUT" : "POST", body: JSON.stringify(payload) }),
  deleteAdmin: (id) => request(`/admins/${id}`, { method: "DELETE" }),
  contact: () => request("/contact"),
  saveContact: (payload) => request("/contact", { method: "PUT", body: JSON.stringify(payload) })
};
