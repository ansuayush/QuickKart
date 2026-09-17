import { useEffect, useState } from "react";
import { Link, Navigate, Route, Routes, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { api, clearSession, getUser, setSession, localWishIds, toggleWish } from "./api";

const LOCAL_IMAGES = {
  Banana: "/images/banana.svg",
  Apple: "/images/apple.svg",
  Tomato: "/images/tomato.svg",
  "Fresh Milk": "/images/milk.svg",
  "Brown Bread": "/images/bread.svg",
  "Potato Chips": "/images/chips.svg",
  "Orange Juice": "/images/juice.svg",
  "Paracetamol 500mg": "/images/medicine.svg",
  "Cetirizine 10mg": "/images/medicine.svg",
  "Antiseptic Cream": "/images/cream.svg",
  "Moisturising Cream": "/images/cream.svg",
  "Body Spray": "/images/spray.svg",
  "Hand Sanitizer": "/images/spray.svg"
};

function ProductImage({ src, name, className = "" }) {
  const [failed, setFailed] = useState(false);
  const local = LOCAL_IMAGES[name] || "/images/grocery.svg";
  const url = failed || !src || src.includes("unsplash.com") ? local : src;
  return <img className={className} src={url} alt={name || ""} onError={() => setFailed(true)} />;
}

function fileToPreview(file) {
  return new Promise((resolve, reject) => {
    const img = new Image();
    const blobUrl = URL.createObjectURL(file);
    img.onload = () => {
      const max = 800;
      const scale = Math.min(1, max / Math.max(img.width, img.height));
      const canvas = document.createElement("canvas");
      canvas.width = Math.max(1, Math.round(img.width * scale));
      canvas.height = Math.max(1, Math.round(img.height * scale));
      canvas.getContext("2d").drawImage(img, 0, 0, canvas.width, canvas.height);
      URL.revokeObjectURL(blobUrl);
      resolve(canvas.toDataURL("image/jpeg", 0.82));
    };
    img.onerror = () => {
      URL.revokeObjectURL(blobUrl);
      reject(new Error("Could not read that image."));
    };
    img.src = blobUrl;
  });
}

function NeedLogin({ mode = "login" }) {
  useEffect(() => { window.dispatchEvent(new CustomEvent("qk-auth", { detail: mode })); }, [mode]);
  return <Navigate to="/" replace />;
}

function isAdmin(user) {
  return (user?.role || "").toLowerCase() === "admin";
}
function isRider(user) {
  return (user?.role || "").toLowerCase() === "deliveryboy";
}

function uidOf(user) {
  return user?.userId || user?.id || "";
}

function useAuth() {
  const [user, setUser] = useState(getUser());
  return { user, setUser };
}

function matchesSearch(product, q) {
  const needle = (q || "").trim().toLowerCase();
  if (!needle) return true;
  const hay = `${product.name || ""} ${product.description || ""} ${product.category?.name || ""} ${product.unit || ""}`.toLowerCase();
  const compact = s => s.replace(/(.)\1+/g, "$1");
  if (hay.includes(needle) || compact(hay).includes(compact(needle))) return true;
  return needle.split(/\s+/).every(t => t.length < 2 || hay.includes(t) || compact(hay).includes(compact(t)));
}

function Header({ user, cartCount, wishCount, onLogout, searchQ, setSearchQ, onAuthOpen }) {
  const nav = useNavigate();
  const [open, setOpen] = useState(false);
  const [acc, setAcc] = useState(false);
  const customer = user && !isAdmin(user) && !isRider(user);
  const go = (e) => {
    e?.preventDefault();
    const query = (searchQ || "").trim();
    nav(query ? `/?q=${encodeURIComponent(query)}` : "/");
    setOpen(false);
  };
  return (
    <header className="header">
      <Link to="/" className="logo" onClick={() => { setSearchQ(""); setOpen(false); setAcc(false); }}>quickkart</Link>
      <div className="locblock">
        <strong>Delivery in 12 minutes</strong>
        <span>Hyderabad, India ▾</span>
      </div>
      <form className="search" onSubmit={go}>
        <span className="sico">⌕</span>
        <input value={searchQ} onChange={e => setSearchQ(e.target.value)} placeholder='Search "bread"' autoComplete="off" />
      </form>
      <button className="menu-btn" type="button" onClick={() => setOpen(o => !o)} aria-label="Menu">☰</button>
      <nav className={`nav ${open ? "open" : ""}`}>
        {user && isRider(user) && (
          <>
            <Link to="/rider" onClick={() => setOpen(false)}>My jobs</Link>
            <button className="linkish" onClick={onLogout}>Logout</button>
          </>
        )}
        {user && isAdmin(user) && (
          <>
            <Link to="/" onClick={() => { setSearchQ(""); setOpen(false); }}>Home</Link>
            <Link to="/dashboard" onClick={() => setOpen(false)}>Dashboard</Link>
            <Link to="/admin" onClick={() => setOpen(false)}>Admin</Link>
            <Link to="/orders" onClick={() => setOpen(false)}>Orders</Link>
            <button className="linkish" onClick={onLogout}>Logout</button>
          </>
        )}
        {!user && (
          <button className="linkish" onClick={() => { setOpen(false); onAuthOpen("login"); }}>Login</button>
        )}
        {!isRider(user) && (
          <Link className="mycart" to="/cart" onClick={() => setOpen(false)}>🛒 My Cart{cartCount ? ` (${cartCount})` : ""}</Link>
        )}
      </nav>
      {customer && (
        <div className="account">
          <button type="button" className="user-ico" onClick={() => setAcc(a => !a)} title="My account">
            {(user.name || "C").trim().charAt(0).toUpperCase()}
          </button>
          {acc && (
            <div className="accmenu">
              <Link to="/dashboard?tab=purchases" onClick={() => setAcc(false)}>Recent purchases</Link>
              <Link to="/orders" onClick={() => setAcc(false)}>My orders</Link>
              <Link to="/cart" onClick={() => setAcc(false)}>My cart ({cartCount})</Link>
              <Link to="/wishlist" onClick={() => setAcc(false)}>Wishlist ({wishCount})</Link>
              <Link to="/dashboard" onClick={() => setAcc(false)}>Dashboard</Link>
              <button type="button" onClick={() => { setAcc(false); onLogout(); }}>Logout</button>
            </div>
          )}
        </div>
      )}
    </header>
  );
}

function Dashboard({ wishCount, cartCount }) {
  const user = getUser();
  const [orders, setOrders] = useState([]);
  const [cart, setCart] = useState(null);
  const [params] = useSearchParams();
  const [tab, setTab] = useState(params.get("tab") || "orders");
  const uid = uidOf(user);
  const tabQ = params.get("tab") || "orders";
  useEffect(() => { setTab(tabQ); }, [tabQ]);
  useEffect(() => {
    if (!uid) return;
    api.orders().then(setOrders).catch(() => setOrders([]));
    api.cart().then(setCart).catch(() => setCart(null));
  }, [uid]);
  if (!user) return <NeedLogin />;
  const purchases = orders
    .filter(o => o.status === "Delivered")
    .flatMap(o => (o.items || []).map(i => ({ ...i, orderId: o.id, orderNumber: o.orderNumber, date: o.createdAt })));
  const active = orders.filter(o => o.status !== "Delivered" && o.status !== "Cancelled");
  return (
    <section className="section dashpage">
      <div className="dashhead">
        <div>
          <p className="muted">{isAdmin(user) ? "Admin account" : "Customer account"}</p>
          <h1>Hi, {user.name}</h1>
          <p>{user.email}</p>
        </div>
        {isAdmin(user) ? (
          <div className="adminacts">
            <Link className="ghost" to="/admin">Admin panel</Link>
            <Link className="cart" to="/">Back to shop</Link>
          </div>
        ) : <Link className="cart" to="/">Shop now</Link>}
      </div>
      <nav className="custmenu">
        <button className={tab === "orders" ? "active" : ""} onClick={() => setTab("orders")}>Orders ({orders.length})</button>
        <button className={tab === "cart" ? "active" : ""} onClick={() => setTab("cart")}>Cart ({cartCount})</button>
        <button className={tab === "purchases" ? "active" : ""} onClick={() => setTab("purchases")}>Recent purchases</button>
        <Link to="/wishlist">Wishlist ({wishCount})</Link>
        {isAdmin(user) && <Link to="/admin">Admin</Link>}
      </nav>
      {tab === "orders" && (
        <div>
          <h2>My orders</h2>
          {active.length > 0 && <p className="muted">{active.length} in progress</p>}
          {orders.length === 0 && <p>No orders yet. <Link to="/">Start shopping</Link></p>}
          {orders.map(o => (
            <Link className="orderline" key={o.id} to={`/orders/${o.id}`}>
              <div>
                <strong>{o.orderNumber}</strong>
                <p>{(o.items || []).map(i => i.productName).join(", ") || "Items"}</p>
                <span className={`badge ${o.status}`}>{o.status}</span>
              </div>
              <span className="cart">Track</span>
            </Link>
          ))}
        </div>
      )}
      {tab === "cart" && (
        <div>
          <h2>My cart</h2>
          {(!cart || cart.items?.length === 0) && <p>Cart is empty. <Link to="/">Add items</Link></p>}
          {cart?.items?.map(i => (
            <div className="orderline" key={i.id}>
              <div>
                <strong>{i.name}</strong>
                <p>{i.quantity} × {i.unit}</p>
              </div>
              <strong>₹{i.lineTotal}</strong>
            </div>
          ))}
          {cart?.items?.length > 0 && <Link className="cart" to="/cart">Go to checkout</Link>}
        </div>
      )}
      {tab === "purchases" && (
        <div>
          <h2>Recent purchases</h2>
          {purchases.length === 0 && <p>No delivered items yet.</p>}
          {purchases.slice(0, 12).map((i, idx) => (
            <div className="orderline" key={idx}>
              <div>
                <strong>{i.productName}</strong>
                <p>{i.quantity} × {i.unit} · {i.orderNumber}</p>
              </div>
              <strong>₹{i.unitPrice * i.quantity}</strong>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

function Home({ searchQ, wishIds }) {
  const [products, setProducts] = useState([]);
  const [all, setAll] = useState([]);
  const [categories, setCategories] = useState([]);
  const [error, setError] = useState("");
  const [categoryId, setCategoryId] = useState("");

  useEffect(() => { api.categories().then(setCategories).catch(() => setError("Could not load categories.")); }, []);
  useEffect(() => {
    api.products("", categoryId || undefined).then(list => {
      setAll(list);
    }).catch(() => setError("Could not load products. Is the API running?"));
  }, [categoryId]);
  useEffect(() => {
    setProducts(searchQ ? all.filter(p => matchesSearch(p, searchQ)) : all);
  }, [all, searchQ]);

  const pickCat = (names) => {
    const hit = categories.find(c => names.some(n => (c.name || "").toLowerCase().includes(n)));
    if (hit) setCategoryId(hit.id);
  };
  const catEmoji = (name) => {
    const n = (name || "").toLowerCase();
    if (n.includes("fruit")) return "🍌";
    if (n.includes("veg")) return "🥬";
    if (n.includes("dairy") || n.includes("milk")) return "🥛";
    if (n.includes("bakery") || n.includes("bread")) return "🍞";
    if (n.includes("snack")) return "🍿";
    if (n.includes("bever")) return "🥤";
    if (n.includes("pharm") || n.includes("medic")) return "💊";
    if (n.includes("care") || n.includes("personal")) return "🧴";
    return "🛒";
  };
  return (
    <>
      <section className="hero">
        <div className="herotxt">
          <h1>Stock up on daily essentials</h1>
          <p>Get farm-fresh goodness & a range of exotic fruits, vegetables, eggs & more</p>
          <button type="button" onClick={() => document.getElementById("products")?.scrollIntoView({ behavior: "smooth" })}>Shop Now</button>
        </div>
        <div className="herofood" aria-hidden="true">
          <span>🥛</span><span>🍎</span><span>🍌</span><span>🥕</span><span>🥦</span><span>🍇</span><span>🥚</span><span>🍊</span>
        </div>
      </section>
      {error && <p className="banner">{error}</p>}
      <section className="section promos">
        <button className="promo teal" type="button" onClick={() => pickCat(["pharm", "medic"])}>
          <div>
            <h3>Pharmacy at your doorstep!</h3>
            <p>Cough syrups, pain relief sprays & more</p>
            <span>Order Now</span>
          </div>
          <b>💊🧴</b>
        </button>
        <button className="promo yellow" type="button" onClick={() => pickCat(["care", "personal"])}>
          <div>
            <h3>Pet care supplies at your door</h3>
            <p>Food, treats, toys & more</p>
            <span>Order Now</span>
          </div>
          <b>🐶🐱</b>
        </button>
        <button className="promo gray" type="button" onClick={() => pickCat(["dairy", "bakery"])}>
          <div>
            <h3>No time for a grocery run?</h3>
            <p>Get daily essentials delivered</p>
            <span>Order Now</span>
          </div>
          <b>🍼</b>
        </button>
      </section>
      <section className="section">
        <div className="catrow">
          <button className={`catpill ${!categoryId ? "active" : ""}`} onClick={() => setCategoryId("")}>
            <i>✨</i><small>All</small>
          </button>
          {categories.map(c => (
            <button key={c.id} className={`catpill ${String(c.id) === String(categoryId) ? "active" : ""}`} onClick={() => setCategoryId(c.id)}>
              <i>{c.icon || catEmoji(c.name)}</i>
              <small>{c.name}</small>
            </button>
          ))}
        </div>
      </section>
      <section className="section" id="products">
        <h2>{searchQ ? `Results for “${searchQ}”` : "Popular products"}</h2>
        {products.length === 0 && <p>No products matched. Try tomato, milk, or paracetamol.</p>}
        <div className="products">
          {products.map(p => <ProductCard key={p.id} p={p} wished={wishIds.includes(p.id)} />)}
        </div>
      </section>
    </>
  );
}

function ProductCard({ p, onAdded, wished }) {
  const nav = useNavigate();
  const user = getUser();
  const add = async () => {
    if (!user) {
      window.dispatchEvent(new CustomEvent("qk-auth", { detail: "login" }));
      return;
    }
    try {
      await api.addToCart(p.id, 1);
      window.dispatchEvent(new Event("qk-cart"));
      onAdded?.();
    } catch (e) { alert(e.message); }
  };
  const heart = async (e) => {
    e.preventDefault();
    e.stopPropagation();
    await toggleWish(p.id, wished);
  };
  return (
    <article className="card">
      <button className={`heart ${wished ? "on" : ""}`} onClick={heart} title="Wishlist">{wished ? "♥" : "♡"}</button>
      <Link to={`/product/${p.id}`}><ProductImage src={p.imageUrl} name={p.name} /></Link>
      <h3>{p.name}</h3>
      <p>{p.unit} · {p.stock > 0 ? `${p.stock} in stock` : "Out of stock"}</p>
      <strong>₹{p.price}</strong>
      <button disabled={p.stock < 1} onClick={add}>ADD</button>
    </article>
  );
}

function ProductPage() {
  const { id } = useParams();
  const [p, setP] = useState(null);
  useEffect(() => { api.product(id).then(setP).catch(console.error); }, [id]);
  if (!p) return <p className="section">Loading...</p>;
  return (
    <section className="section detail">
      <ProductImage src={p.imageUrl} name={p.name} />
      <div>
        <p className="muted">{p.category?.name}</p>
        <h1>{p.name}</h1>
        <p>{p.description}</p>
        <p>{p.unit}</p>
        <h2>₹{p.price}</h2>
        <ProductCard p={p} />
      </div>
    </section>
  );
}

function AuthModal({ onAuth, startMode = "login", onClose }) {
  const [mode, setMode] = useState(startMode);
  const [form, setForm] = useState({ name: "", email: "", phone: "", password: "" });
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  useEffect(() => { setMode(startMode); setError(""); }, [startMode]);
  const submit = async (e) => {
    e.preventDefault();
    setBusy(true);
    setError("");
    try {
      const data = mode === "login" ? await api.login(form) : await api.register(form);
      setSession(data);
      onAuth(getUser());
      onClose();
    } catch (err) { setError(err.message); }
    finally { setBusy(false); }
  };
  const set = (k) => (e) => setForm({ ...form, [k]: e.target.value });
  return (
    <div className="modal-back" onClick={onClose}>
      <div className="authcard" onClick={e => e.stopPropagation()}>
        <aside className="authbrand">
          <div className="authmark">QK</div>
          <h2>QuickKart</h2>
          <p>Fresh groceries at your door in minutes.</p>
          <ul>
            <li>Fast delivery</li>
            <li>Secure checkout</li>
            <li>Track every order</li>
          </ul>
        </aside>
        <div className="authform">
          <button type="button" className="modal-x" onClick={onClose} aria-label="Close">×</button>
          <h1>{mode === "login" ? "Welcome back" : "Create your account"}</h1>
          <p className="muted">{mode === "login" ? "Sign in to continue shopping." : "Join QuickKart in less than a minute."}</p>
          <div className="tabs">
            <button type="button" className={mode === "login" ? "active" : ""} onClick={() => { setMode("login"); setError(""); }}>Login</button>
            <button type="button" className={mode === "register" ? "active" : ""} onClick={() => { setMode("register"); setError(""); }}>Sign up</button>
          </div>
          <form onSubmit={submit}>
            {error && <p className="error">{error}</p>}
            {mode === "register" && <>
              <label>Full name<input value={form.name} onChange={set("name")} required placeholder="Anita Sharma" /></label>
              <label>Phone<input value={form.phone} onChange={set("phone")} required placeholder="98765 43210" /></label>
            </>}
            <label>Email<input type="email" value={form.email} onChange={set("email")} required placeholder="you@email.com" /></label>
            <label>Password<input type="password" value={form.password} onChange={set("password")} required minLength={6} placeholder="Min. 6 characters" /></label>
            <button className="go" type="submit" disabled={busy}>{busy ? "Please wait..." : mode === "login" ? "Login to QuickKart" : "Create account"}</button>
          </form>
          <p className="demo">Customer: customer@quickkart.local / Customer@123<br />Admin: admin@quickkart.local / Admin@123<br />Rider: ravi@quickkart.local / Rider@123</p>
        </div>
      </div>
    </div>
  );
}

function WishlistPage({ wishIds }) {
  const user = getUser();
  const [items, setItems] = useState([]);
  useEffect(() => {
    api.products().then(list => setItems(list.filter(p => wishIds.includes(p.id)))).catch(console.error);
  }, [wishIds]);
  const moveToCart = async (p) => {
    if (!user) {
      window.dispatchEvent(new CustomEvent("qk-auth", { detail: "login" }));
      return;
    }
    await api.addToCart(p.id, 1);
    await toggleWish(p.id, true);
    window.dispatchEvent(new Event("qk-cart"));
  };
  const total = items.reduce((s, p) => s + Number(p.price || 0), 0);
  return (
    <section className="section wishpage">
      <div className="wishhero">
        <div>
          <p>Saved for later</p>
          <h1>Your wishlist</h1>
          <span>{items.length} item{items.length === 1 ? "" : "s"}{items.length ? ` · ₹${total}` : ""}</span>
        </div>
        <Link className="ghost-link" to="/">Continue shopping</Link>
      </div>
      {items.length === 0 && (
        <div className="wishempty">
          <div className="empty-heart">♡</div>
          <h2>No favourites yet</h2>
          <p>Tap the heart on any product to save it here for later.</p>
          <Link className="cart" to="/">Browse products</Link>
        </div>
      )}
      <div className="wishlist">
        {items.map(p => (
          <article className="wishrow" key={p.id}>
            <Link to={`/product/${p.id}`} className="wishpic"><ProductImage src={p.imageUrl} name={p.name} /></Link>
            <div className="wishinfo">
              <h3>{p.name}</h3>
              <p>{p.unit} · {p.stock > 0 ? "In stock" : "Out of stock"}</p>
              <strong>₹{p.price}</strong>
            </div>
            <div className="wishacts">
              <button className="go" disabled={p.stock < 1} onClick={() => moveToCart(p)}>Move to cart</button>
              <button className="ghost" onClick={() => toggleWish(p.id, true)}>Remove</button>
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}

function CartPage() {
  const user = getUser();
  const [cart, setCart] = useState(null);
  const [error, setError] = useState("");
  const load = () => api.cart().then(setCart).catch(e => setError(e.message));
  useEffect(() => { if (uidOf(user)) load(); }, [uidOf(user)]);
  if (!user) return <NeedLogin />;
  if (!cart) return <p className="section">{error || "Loading cart..."}</p>;
  const change = async (productId, quantity) => {
    await api.updateCart(productId, quantity);
    window.dispatchEvent(new Event("qk-cart"));
    load();
  };
  return (
    <section className="section">
      <h2>Your cart</h2>
      {cart.items.length === 0 && <p>Cart is empty. <Link to="/">Shop now</Link></p>}
      {cart.items.map(i => (
        <div className="line" key={i.id}>
          <ProductImage src={i.imageUrl} name={i.name} />
          <div>
            <strong>{i.name}</strong>
            <p>{i.unit}</p>
            <div className="qty">
              <button onClick={() => change(i.productId, i.quantity - 1)}>-</button>
              <span>{i.quantity}</span>
              <button onClick={() => change(i.productId, i.quantity + 1)}>+</button>
            </div>
          </div>
          <strong>₹{i.lineTotal}</strong>
        </div>
      ))}
      {cart.items.length > 0 && (
        <aside className="summary">
          <p>Subtotal <span>₹{cart.subtotal}</span></p>
          <p>Delivery <span>{cart.deliveryFee ? `₹${cart.deliveryFee}` : "FREE"}</span></p>
          <h3>Total <span>₹{cart.total}</span></h3>
          <Link className="cart" to="/checkout">Checkout</Link>
        </aside>
      )}
    </section>
  );
}

function CheckoutPage() {
  const user = getUser();
  const nav = useNavigate();
  const [cart, setCart] = useState(null);
  const [addresses, setAddresses] = useState([]);
  const [addressId, setAddressId] = useState("");
  const [paymentMethod, setPaymentMethod] = useState("COD");
  const [upiId, setUpiId] = useState("customer@upi");
  const [card, setCard] = useState({ number: "4111111111111111", expiry: "12/29", cvv: "123" });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [addr, setAddr] = useState({ label: "Home", line1: "", city: "Bengaluru", pincode: "560001", phone: "", isDefault: true, latitude: 12.9716, longitude: 77.5946 });
  const [preferred, setPreferred] = useState(null);
  const load = async () => {
    const [c, a, p] = await Promise.all([
      api.cart(),
      api.addresses(),
      api.preferredRider().catch(() => null)
    ]);
    setCart(c);
    setAddresses(a);
    setPreferred(p);
    if (a[0]) setAddressId(a[0].id);
  };
  useEffect(() => { if (uidOf(user)) load().catch(console.error); }, [uidOf(user)]);
  if (!user) return <NeedLogin />;
  const pinLocation = () => {
    if (!navigator.geolocation) return alert("Location is not available in this browser.");
    navigator.geolocation.getCurrentPosition(
      pos => setAddr(x => ({ ...x, latitude: pos.coords.latitude, longitude: pos.coords.longitude })),
      () => alert("Could not read GPS. You can still save the address.")
    );
  };
  const saveAddr = async (e) => {
    e.preventDefault();
    const created = await api.addAddress(addr);
    await load();
    setAddressId(created.id);
  };
  const place = async () => {
    setError("");
    setBusy(true);
    try {
      let id = addressId;
      if (!id) {
        if (!addr.line1 || !addr.city || !addr.pincode || !addr.phone) {
          throw new Error("Fill the address and click Place order again, or click Save address first.");
        }
        const created = await api.addAddress(addr);
        id = created.id;
        setAddressId(id);
        setAddresses(list => [created, ...list]);
      }
      let paymentRef = "";
      if (paymentMethod === "UPI" || paymentMethod === "CARD") {
        const paid = await api.charge({
          method: paymentMethod,
          amount: cart.total,
          upiId,
          cardNumber: card.number,
          expiry: card.expiry,
          cvv: card.cvv
        });
        paymentRef = paid.paymentRef;
      }
      const order = await api.checkout({ addressId: Number(id), paymentMethod, paymentRef });
      window.dispatchEvent(new Event("qk-cart"));
      nav(`/orders/${order.id}`);
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  };
  if (!cart) return <p className="section">Loading...</p>;
  const upiLink = `upi://pay?pa=${encodeURIComponent(upiId)}&pn=QuickKart&am=${cart.total}&cu=INR&tn=QuickKart%20order`;
  return (
    <section className="section split">
      <div>
        <h2>Delivery address</h2>
        {addresses.map(a => (
          <label className="choice" key={a.id}>
            <input type="radio" checked={String(addressId) === String(a.id)} onChange={() => setAddressId(a.id)} />
            <span><strong>{a.label}</strong> — {a.line1}, {a.city} {a.pincode}</span>
          </label>
        ))}
        <form className="panel nested" onSubmit={saveAddr}>
          <h3>Add address</h3>
          <label>Label<input value={addr.label} onChange={e => setAddr({ ...addr, label: e.target.value })} /></label>
          <label>Street<input value={addr.line1} onChange={e => setAddr({ ...addr, line1: e.target.value })} required /></label>
          <label>City<input value={addr.city} onChange={e => setAddr({ ...addr, city: e.target.value })} required /></label>
          <label>Pincode<input value={addr.pincode} onChange={e => setAddr({ ...addr, pincode: e.target.value })} required /></label>
          <label>Phone<input value={addr.phone} onChange={e => setAddr({ ...addr, phone: e.target.value })} required /></label>
          <button type="button" className="ghost" onClick={pinLocation}>Use current GPS location</button>
          <button>Save address</button>
        </form>
        <h2>Pay with UPI or card (free test gateway)</h2>
        {["UPI", "CARD", "COD"].map(m => (
          <label className="choice" key={m}>
            <input type="radio" checked={paymentMethod === m} onChange={() => setPaymentMethod(m)} />
            {m === "COD" ? "Cash on delivery" : m === "UPI" ? "UPI (GPay / PhonePe / BHIM)" : "Debit / credit card"}
          </label>
        ))}
        {paymentMethod === "UPI" && (
          <div className="paybox">
            <p>Scan this UPI QR or pay to <strong>{upiId}</strong></p>
            <img alt="UPI QR" src={`https://api.qrserver.com/v1/create-qr-code/?size=180x180&data=${encodeURIComponent(upiLink)}`} />
            <label>UPI ID<input value={upiId} onChange={e => setUpiId(e.target.value)} /></label>
            <p className="muted">Sandbox only — no money is taken from your bank.</p>
          </div>
        )}
        {paymentMethod === "CARD" && (
          <div className="paybox">
            <label>Card number<input value={card.number} onChange={e => setCard({ ...card, number: e.target.value })} /></label>
            <div className="split-mini">
              <label>Expiry<input value={card.expiry} onChange={e => setCard({ ...card, expiry: e.target.value })} placeholder="MM/YY" /></label>
              <label>CVV<input value={card.cvv} onChange={e => setCard({ ...card, cvv: e.target.value })} /></label>
            </div>
            <p className="muted">Test card: 4111 1111 1111 1111 · any future expiry · any 3-digit CVV. No real charge.</p>
          </div>
        )}
        {error && <p className="error">{error}</p>}
      </div>
      <aside className="summary">
        <p>Items <span>{cart.items.length}</span></p>
        <p>Total <span>₹{cart.total}</span></p>
        {preferred?.name && (
          <div className="preferbox">
            <strong>Preferred delivery partner</strong>
            <p>{preferred.name} · {preferred.vehicle}</p>
            <p className="muted">We’ll assign {preferred.name} again if they are available.</p>
          </div>
        )}
        {error && <p className="error">{error}</p>}
        <p className="muted">For a quick test keep Cash on delivery selected, fill the address, then place the order.</p>
        <button className="cart" disabled={cart.items.length === 0 || busy} onClick={place}>
          {busy ? "Processing..." : paymentMethod === "COD" ? "Place order" : `Pay ₹${cart.total}`}
        </button>
      </aside>
    </section>
  );
}

function OrdersPage() {
  const user = getUser();
  const [orders, setOrders] = useState([]);
  useEffect(() => { if (uidOf(user)) api.orders().then(setOrders).catch(console.error); }, [uidOf(user)]);
  if (!user) return <NeedLogin />;
  return (
    <section className="section">
      <h2>{isAdmin(user) ? "Track any order" : "Your orders"}</h2>
      <p className="muted">{isAdmin(user) ? "Open any order to live-track rider, timing, and issues." : "Open an order to track delivery in real time."}</p>
      {orders.length === 0 && <p>No orders yet.</p>}
      {orders.map(o => (
        <div className="line" key={o.id}>
          <div>
            <strong>{o.orderNumber}</strong>
            <p>{o.status} · {o.user?.name || o.paymentStatus} · {new Date(o.createdAt).toLocaleString()}</p>
          </div>
          <Link className="cart" to={`/orders/${o.id}`}>Track</Link>
        </div>
      ))}
    </section>
  );
}

function OrderDetail() {
  const { id } = useParams();
  const user = getUser();
  const [order, setOrder] = useState(null);
  const [loadErr, setLoadErr] = useState("");
  const [track, setTrack] = useState(null);
  const [riders, setRiders] = useState([]);
  const [stars, setStars] = useState(5);
  const [review, setReview] = useState("");
  const [prefer, setPrefer] = useState(true);
  const [msg, setMsg] = useState("");
  const load = () => api.order(id).then(o => { setOrder(o); setLoadErr(""); }).catch(e => setLoadErr(e.message));
  useEffect(() => { load(); }, [id]);
  useEffect(() => {
    if (isAdmin(user)) api.availableRiders(id).then(setRiders).catch(() => api.deliveryBoys().then(setRiders).catch(() => {}));
  }, [uidOf(user), id]);
  useEffect(() => {
    const poll = () => api.tracking(id).then(setTrack).catch(() => {});
    poll();
    const timer = setInterval(poll, 4000);
    return () => clearInterval(timer);
  }, [id]);
  if (loadErr) return (
    <p className="section error">{loadErr}{" "}
      <Link to={isRider(user) ? "/rider" : isAdmin(user) ? "/orders" : "/orders"}>Back</Link>
    </p>
  );
  if (!order) return <p className="section">Loading...</p>;
  const statuses = ["Placed", "Confirmed", "Picking", "Packed", "Assigned", "Accepted", "ArrivedAtStore", "PickedUp", "OutForDelivery", "Delivered", "Cancelled"];
  const flow = ["Placed", "Confirmed", "Picking", "Packed", "Assigned", "Accepted", "PickedUp", "OutForDelivery", "Delivered"];
  const boy = order.deliveryBoy;
  const statusNow = track?.status || order.status;
  const stepAt = flow.indexOf(statusNow);
  const customerView = !isAdmin(user) && !isRider(user);
  const rate = async (e) => {
    e.preventDefault();
    try {
      await api.rateOrder(order.id, stars, review, prefer && delivered);
      setMsg(prefer && delivered ? "Thanks. We’ll try to send the same delivery partner next time." : "Thanks for your rating.");
      load();
    } catch (err) { setMsg(err.message); }
  };
  const saveSameBoy = async () => {
    try {
      await api.preferRider(order.id);
      setMsg(`Saved. ${boy?.name || "This partner"} will be assigned on your next order if available.`);
      load();
    } catch (err) { setMsg(err.message); }
  };
  const delivered = (track?.status || order.status) === "Delivered";
  return (
    <section className="section">
      <h2>Track order {order.orderNumber}</h2>
      {isAdmin(user) && <p className="muted">Admin live view · Customer {order.user?.name || order.userId}</p>}
      <p>Status: <strong>{statusNow}</strong> · Payment: {order.paymentMethod} ({order.paymentStatus}{order.paymentRef ? ` · ${order.paymentRef}` : ""})</p>
      <div className="trackmeta">
        {track?.onTime === true && <span className="badge Delivered">On time</span>}
        {track?.onTime === false && <span className="badge Cancelled">Delayed</span>}
        <span className="muted">Elapsed {track?.elapsedMinutes ?? 0} min · Promise {track?.slaMinutes ?? 12} min · ETA ~ {track?.etaMinutes ?? "—"} min</span>
      </div>
      {track?.issue && <p className="error">{track.issue}</p>}
      <div className="stepper">
        {flow.map((s, i) => (
          <span key={s} className={`step ${i <= stepAt && statusNow !== "Cancelled" ? "done" : ""} ${s === statusNow ? "now" : ""}`}>{s}</span>
        ))}
      </div>
      <p>{order.address?.line1}, {order.address?.city} {order.address?.pincode}</p>
      {boy && (
        <div className="ridercard">
          <img className="riderpic" src={boy.photoUrl || "/images/rider.svg"} alt="" width="64" height="64" />
          <div>
            <strong>{boy.name}</strong>
            <p>{boy.vehicle}</p>
            <p>Phone: {boy.phone} · Rider rating {boy.ratingAverage?.toFixed?.(1) ?? boy.ratingAverage}/5</p>
            {order.preferSameRider && <p className="badge">Preferred for next delivery</p>}
          </div>
        </div>
      )}
      {!boy && <p className="muted">A delivery partner will be assigned shortly.</p>}
      {track && (
        <div className="mapwrap">
          <div className="mapbar">
            <span>Rider ETA ~ {track.etaMinutes} min</span>
            <a href={track.googleMapsLink} target="_blank" rel="noreferrer">Open in Google Maps</a>
          </div>
          <iframe title="Delivery map" src={track.mapUrl} allowFullScreen loading="lazy" />
        </div>
      )}
      {order.items.map(i => (
        <div className="line" key={i.id}>
          <div><strong>{i.productName}</strong><p>{i.quantity} × {i.unit}</p></div>
          <strong>₹{i.unitPrice * i.quantity}</strong>
        </div>
      ))}
      <h3>Total ₹{order.total}</h3>
      {order.rating ? (
        <p>Your rating: {"★".repeat(order.rating)}{"☆".repeat(5 - order.rating)} {order.review}</p>
      ) : customerView && delivered && (
        <form className="panel nested" onSubmit={rate}>
          <h3>Rate this delivery</h3>
          <div className="stars">
            {[1, 2, 3, 4, 5].map(n => (
              <button type="button" key={n} className={n <= stars ? "on" : ""} onClick={() => setStars(n)}>★</button>
            ))}
          </div>
          <label>Comment<input value={review} onChange={e => setReview(e.target.value)} placeholder="How was the delivery?" /></label>
          {delivered && boy && (
            <label className="choice">
              <input type="checkbox" checked={prefer} onChange={e => setPrefer(e.target.checked)} />
              I liked this partner — send {boy.name} again next time
            </label>
          )}
          <button>Submit rating</button>
          {msg && <p className="muted">{msg}</p>}
        </form>
      )}
      {delivered && boy && !order.preferSameRider && order.rating && (
        <div className="panel nested">
          <h3>Want the same delivery partner?</h3>
          <p>{boy.name} delivered this order. Save them for your next grocery run.</p>
          <button type="button" onClick={saveSameBoy}>Deliver with {boy.name} again</button>
          {msg && <p className="muted">{msg}</p>}
        </div>
      )}
      {isRider(user) && !["Delivered", "Cancelled"].includes(order.status) && (
        <div className="adminacts" style={{ margin: "16px 0" }}>
          {order.status === "Assigned" && <button type="button" className="cart" onClick={() => api.riderAccept(order.id).then(load)}>Accept</button>}
          {(order.status === "Accepted" || order.status === "ArrivedAtStore") && <button type="button" onClick={() => api.riderPickup(order.id).then(load)}>Picked up</button>}
          {order.status === "PickedUp" && <button type="button" onClick={() => api.riderStart(order.id).then(load)}>Out for delivery</button>}
          {order.status === "OutForDelivery" && <button type="button" className="cart" onClick={() => api.riderComplete(order.id).then(load)}>Delivered</button>}
          <Link to="/rider">Back to jobs</Link>
        </div>
      )}
      {user?.role === "Admin" && (
        <div className="panel nested">
          <h3>Admin: assign nearby rider</h3>
          <div className="categories">
            {riders.map(r => (
              <button key={r.id} onClick={() => api.assignRider(order.id, r.id).then(load)}>
                {r.name} · {r.km != null ? `${r.km} km` : r.vehicle} · {r.dutyStatus || (r.isAvailable ? "Available" : "Busy")}
              </button>
            ))}
          </div>
          <div className="categories">
            {statuses.map(s => <button key={s} onClick={() => api.updateOrderStatus(order.id, s).then(load)}>{s}</button>)}
          </div>
        </div>
      )}
    </section>
  );
}

function RiderPage() {
  const user = getUser();
  const [jobs, setJobs] = useState([]);
  const [msg, setMsg] = useState("");
  const load = () => api.riderJobs().then(setJobs).catch(e => setMsg(e.message));
  useEffect(() => {
    if (!isRider(user) && !isAdmin(user)) return;
    load();
    const t = setInterval(load, 5000);
    return () => clearInterval(t);
  }, [uidOf(user)]);
  if (!user) return <NeedLogin />;
  if (!isRider(user) && !isAdmin(user)) return <Navigate to="/" />;
  const incoming = jobs.filter(o => o.status === "Assigned");
  const active = jobs.filter(o => !["Delivered", "Cancelled", "Assigned"].includes(o.status));
  const done = jobs.filter(o => o.status === "Delivered").slice(0, 8);
  const act = async (fn, id) => {
    try { await fn(id); setMsg(""); load(); } catch (e) { setMsg(e.message); }
  };
  const card = (o, actions) => (
    <article className="jobcard" key={o.id}>
      <p className="muted">NEW ORDER</p>
      <h2>{o.orderNumber}</h2>
      <p>Customer {o.user?.name || "Customer"}</p>
      <p>Items {(o.items || []).reduce((s, i) => s + i.quantity, 0)} · Amount ₹{o.total}</p>
      <p>Pickup QuickKart store, HITEC City</p>
      <p>Delivery {o.address?.line1}, {o.address?.city}</p>
      <span className={`badge ${o.status}`}>{o.status}</span>
      <div className="adminacts">{actions}</div>
    </article>
  );
  return (
    <section className="section">
      <h1>Delivery jobs</h1>
      <p className="muted">Accept nearby assignments, pick up from the store, then deliver.</p>
      {msg && <p className="error">{msg}</p>}
      <h2>Waiting for you</h2>
      {incoming.length === 0 && <p>No new assignments right now.</p>}
      {incoming.map(o => card(o, (
        <>
          <button type="button" className="cart" onClick={() => act(api.riderAccept, o.id)}>Accept</button>
          <button type="button" className="danger" onClick={() => act(api.riderReject, o.id)}>Reject</button>
        </>
      )))}
      <h2>In progress</h2>
      {active.map(o => card(o, (
        <>
          {o.status === "Accepted" && <button type="button" onClick={() => act(api.riderArrive, o.id)}>Arrived at store</button>}
          {(o.status === "Accepted" || o.status === "ArrivedAtStore") && <button type="button" onClick={() => act(api.riderPickup, o.id)}>Picked up</button>}
          {o.status === "PickedUp" && <button type="button" onClick={() => act(api.riderStart, o.id)}>Out for delivery</button>}
          {o.status === "OutForDelivery" && <button type="button" className="cart" onClick={() => act(api.riderComplete, o.id)}>Delivered</button>}
          <Link to={`/orders/${o.id}`}>Track</Link>
        </>
      )))}
      <h2>Completed</h2>
      {done.map(o => (
        <div className="line" key={o.id}>
          <div><strong>{o.orderNumber}</strong><p>{o.status}</p></div>
          <strong>₹{o.total}</strong>
        </div>
      ))}
    </section>
  );
}

function AdminPage() {
  const user = getUser();
  const blank = { id: null, name: "", description: "", price: 10, unit: "1 pc", imageUrl: "", stock: 10, isActive: true, categoryId: "" };
  const [categories, setCategories] = useState([]);
  const [products, setProducts] = useState([]);
  const [orders, setOrders] = useState([]);
  const [riders, setRiders] = useState([]);
  const [admins, setAdmins] = useState([]);
  const [form, setForm] = useState(blank);
  const [contact, setContact] = useState(FALLBACK_CONTACT);
  const blankRider = { id: null, name: "", phone: "", vehicleType: "Bike", vehicleNumber: "", email: "", password: "", isAvailable: true };
  const blankAdmin = { id: null, name: "", email: "", phone: "", password: "" };
  const [riderForm, setRiderForm] = useState(blankRider);
  const [adminForm, setAdminForm] = useState(blankAdmin);
  const [busy, setBusy] = useState(false);
  const load = async () => {
    const [c, p, o, r, info, a] = await Promise.all([
      api.categories(), api.products(), api.orders(), api.deliveryBoys(),
      api.contact().catch(() => loadStoredContact() || FALLBACK_CONTACT),
      api.admins().catch(() => [])
    ]);
    setCategories(c);
    setProducts(p);
    setOrders(o);
    setRiders(r);
    setAdmins(a);
    setContact(normalizeContact(info));
    setForm(f => (!f.categoryId && c[0] ? { ...f, categoryId: c[0].id } : f));
  };
  useEffect(() => { load().catch(console.error); }, []);
  if (!user || !isAdmin(user)) return <Navigate to="/" />;
  const save = async (e) => {
    e.preventDefault();
    setBusy(true);
    try {
      const payload = { ...form, price: Number(form.price), stock: Number(form.stock), categoryId: Number(form.categoryId), isActive: true };
      await api.saveProduct(payload, form.id);
      setForm({ ...blank, categoryId: form.categoryId || categories[0]?.id || "" });
      await load();
    } catch (err) {
      alert(err.message);
    } finally {
      setBusy(false);
    }
  };
  const edit = (p) => {
    setForm({
      id: p.id,
      name: p.name || "",
      description: p.description || "",
      price: p.price,
      unit: p.unit || "1 pc",
      imageUrl: p.imageUrl || "",
      stock: p.stock,
      isActive: p.isActive !== false,
      categoryId: p.categoryId || p.category?.id || categories[0]?.id || ""
    });
    window.scrollTo({ top: 0, behavior: "smooth" });
  };
  const remove = async (p) => {
    if (!window.confirm(`Delete “${p.name}”? It will be hidden from the shop.`)) return;
    try {
      await api.deleteProduct(p.id);
      if (form.id === p.id) setForm({ ...blank, categoryId: form.categoryId });
      await load();
    } catch (err) {
      alert(err.message);
    }
  };
  return (
    <section className="section">
      <div className="dashhead">
        <div>
          <p className="muted">Admin</p>
          <h1>Store dashboard</h1>
        </div>
        <div className="adminacts">
          <Link className="ghost" to="/dashboard">Dashboard</Link>
          <Link className="cart" to="/">Back to shop</Link>
        </div>
      </div>
      <form className="panel nested contactadmin" onSubmit={async e => {
        e.preventDefault();
        try {
          localStorage.setItem("qk_contact", JSON.stringify(contact));
          await api.saveContact(contact);
          alert("Contact details saved. They now show on Contact us.");
        } catch {
          alert("Contact details saved on this site. Restart the API later to store them on the server.");
        }
      }}>
        <h2>Contact us page</h2>
        <p className="muted">These details appear at /contact and in the footer link.</p>
        <label>Company<input value={contact.company} onChange={e => setContact({ ...contact, company: e.target.value })} /></label>
        <label>Phone<input value={contact.phone} onChange={e => setContact({ ...contact, phone: e.target.value })} /></label>
        <label>Email<input value={contact.email} onChange={e => setContact({ ...contact, email: e.target.value })} /></label>
        <label>WhatsApp<input value={contact.whatsApp} onChange={e => setContact({ ...contact, whatsApp: e.target.value })} /></label>
        <label>Hours<input value={contact.hours} onChange={e => setContact({ ...contact, hours: e.target.value })} /></label>
        <label>Address<input value={contact.address} onChange={e => setContact({ ...contact, address: e.target.value })} /></label>
        <label>Note<input value={contact.note} onChange={e => setContact({ ...contact, note: e.target.value })} /></label>
        <button type="submit">Save contact details</button>
      </form>
      <div className="split">
        <form className="panel nested" onSubmit={async e => {
          e.preventDefault();
          try {
            await api.saveAdmin({ name: adminForm.name, email: adminForm.email, phone: adminForm.phone, password: adminForm.password || undefined }, adminForm.id);
            setAdminForm(blankAdmin);
            await load();
          } catch (err) { alert(err.message); }
        }}>
          <h2>{adminForm.id ? "Edit admin" : "Add admin"}</h2>
          <p className="muted">Only Admin role. Customers cannot see this.</p>
          <label>Name<input value={adminForm.name} onChange={e => setAdminForm({ ...adminForm, name: e.target.value })} required /></label>
          <label>Email<input type="email" value={adminForm.email} onChange={e => setAdminForm({ ...adminForm, email: e.target.value })} required /></label>
          <label>Phone<input value={adminForm.phone} onChange={e => setAdminForm({ ...adminForm, phone: e.target.value })} /></label>
          <label>{adminForm.id ? "New password (optional)" : "Password"}<input type="password" value={adminForm.password} onChange={e => setAdminForm({ ...adminForm, password: e.target.value })} required={!adminForm.id} minLength={adminForm.id ? undefined : 6} /></label>
          <button type="submit">{adminForm.id ? "Update admin" : "Create admin"}</button>
          {adminForm.id && <button type="button" className="ghost" onClick={() => setAdminForm(blankAdmin)}>Cancel</button>}
        </form>
        <div>
          <h2>Admin users</h2>
          {admins.map(a => (
            <div className="line" key={a.id}>
              <div>
                <strong>{a.name}</strong>
                <p>{a.email} · {a.phone || "No phone"} · {a.role}</p>
              </div>
              <div className="adminacts">
                <button type="button" className="ghost" onClick={() => setAdminForm({ id: a.id, name: a.name, email: a.email, phone: a.phone || "", password: "" })}>Edit</button>
                <button type="button" className="danger" onClick={async () => {
                  if (!window.confirm(`Delete admin ${a.name}?`)) return;
                  try { await api.deleteAdmin(a.id); await load(); } catch (err) { alert(err.message); }
                }}>Delete</button>
              </div>
            </div>
          ))}
        </div>
      </div>
      <div className="split">
        <form className="panel nested" onSubmit={async e => {
          e.preventDefault();
          try {
            await api.saveRider({
              name: riderForm.name,
              phone: riderForm.phone,
              vehicleType: riderForm.vehicleType,
              vehicleNumber: riderForm.vehicleNumber,
              email: riderForm.email,
              password: riderForm.password || undefined,
              isAvailable: riderForm.isAvailable
            }, riderForm.id);
            setRiderForm(blankRider);
            await load();
          } catch (err) { alert(err.message); }
        }}>
          <h2>{riderForm.id ? "Edit delivery partner" : "Add delivery partner"}</h2>
          <label>Name<input value={riderForm.name} onChange={e => setRiderForm({ ...riderForm, name: e.target.value })} required /></label>
          <label>Phone<input value={riderForm.phone} onChange={e => setRiderForm({ ...riderForm, phone: e.target.value })} required /></label>
          <label>Vehicle type<input value={riderForm.vehicleType} onChange={e => setRiderForm({ ...riderForm, vehicleType: e.target.value })} /></label>
          <label>Vehicle number<input value={riderForm.vehicleNumber} onChange={e => setRiderForm({ ...riderForm, vehicleNumber: e.target.value })} /></label>
          <label>Login email<input type="email" value={riderForm.email} onChange={e => setRiderForm({ ...riderForm, email: e.target.value })} placeholder="ravi@quickkart.local" /></label>
          <label>{riderForm.id ? "New password (optional)" : "Login password"}<input type="password" value={riderForm.password} onChange={e => setRiderForm({ ...riderForm, password: e.target.value })} /></label>
          <label className="choice"><input type="checkbox" checked={riderForm.isAvailable} onChange={e => setRiderForm({ ...riderForm, isAvailable: e.target.checked })} /> Available for orders</label>
          <button type="submit">{riderForm.id ? "Update partner" : "Add partner"}</button>
          {riderForm.id && <button type="button" className="ghost" onClick={() => setRiderForm(blankRider)}>Cancel</button>}
        </form>
        <div>
          <h2>Delivery partners</h2>
          {riders.map(r => (
            <div className="line" key={r.id}>
              <div>
                <strong>{r.name}</strong>
                <p>{r.phone} · {r.vehicle || `${r.vehicleType} ${r.vehicleNumber}`} · {r.ratingAverage}/5 ({r.ratingCount})</p>
              </div>
              <div className="adminacts">
                <span>{r.isAvailable ? "Available" : "Busy"}</span>
                <button type="button" className="ghost" onClick={() => setRiderForm({
                  id: r.id, name: r.name, phone: r.phone || "", vehicleType: r.vehicleType || "Bike",
                  vehicleNumber: r.vehicleNumber || "", email: "", password: "", isAvailable: r.isAvailable !== false
                })}>Edit</button>
                <button type="button" className="danger" onClick={async () => {
                  if (!window.confirm(`Delete ${r.name}?`)) return;
                  try { await api.deleteRider(r.id); await load(); } catch (err) { alert(err.message); }
                }}>Delete</button>
              </div>
            </div>
          ))}
        </div>
      </div>
      <h2>Open orders</h2>
      {orders.map(o => (
        <div className="line" key={o.id}>
          <div>
            <Link to={`/orders/${o.id}`}><strong>{o.orderNumber}</strong></Link>
            <p>{o.status} · {o.user?.name || "Customer"} · {o.deliveryBoy?.name || "Unassigned"}</p>
          </div>
          <div className="adminacts">
            <Link className="cart" to={`/orders/${o.id}`}>Track</Link>
            <select value={o.deliveryBoyId || ""} onChange={e => api.assignRider(o.id, Number(e.target.value)).then(load)}>
              <option value="">Assign rider</option>
              {riders.map(r => <option key={r.id} value={r.id}>{r.name}{r.dutyStatus ? ` · ${r.dutyStatus}` : ""}</option>)}
            </select>
          </div>
        </div>
      ))}
      <div className="split">
      <form className="panel nested" onSubmit={save}>
        <h2>{form.id ? "Edit product" : "Add product"}</h2>
        <label>Name<input value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} required /></label>
        <label>Description<input value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} /></label>
        <label>Price<input type="number" value={form.price} onChange={e => setForm({ ...form, price: e.target.value })} /></label>
        <label>Unit<input value={form.unit} onChange={e => setForm({ ...form, unit: e.target.value })} /></label>
        <label>Stock<input type="number" value={form.stock} onChange={e => setForm({ ...form, stock: e.target.value })} /></label>
        <label className="imgup">Product image
          <div className="imgpreview">
            {form.imageUrl ? <img src={form.imageUrl} alt="Preview" /> : <span>No image — upload to preview</span>}
          </div>
          <input type="file" accept="image/*" onChange={async e => {
            const file = e.target.files?.[0];
            e.target.value = "";
            if (!file) return;
            try {
              const dataUrl = await fileToPreview(file);
              setForm(f => ({ ...f, imageUrl: dataUrl }));
            } catch (err) {
              alert(err.message);
            }
          }} />
        </label>
        <label>Or image URL<input value={form.imageUrl.startsWith("data:") ? "" : form.imageUrl} onChange={e => setForm({ ...form, imageUrl: e.target.value })} placeholder="https://… or /images/…" /></label>
        <label>Category
          <select value={form.categoryId} onChange={e => setForm({ ...form, categoryId: e.target.value })}>
            {categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
        </label>
        <button disabled={busy}>{form.id ? "Update" : "Save"}</button>
        {form.id && <button type="button" className="ghost" onClick={() => setForm({ ...blank, categoryId: form.categoryId })}>Cancel edit</button>}
      </form>
      <div>
        <h2>Inventory</h2>
        {products.map(p => (
          <div className="line" key={p.id}>
            <ProductImage className="adminthumb" src={p.imageUrl} name={p.name} />
            <div>
              <strong>{p.name}</strong>
              <p>Stock {p.stock} · {p.unit}</p>
            </div>
            <div className="adminacts">
              <strong>₹{p.price}</strong>
              <button type="button" className="ghost" onClick={() => edit(p)}>Edit</button>
              <button type="button" className="danger" onClick={() => remove(p)}>Delete</button>
            </div>
          </div>
        ))}
      </div>
      </div>
    </section>
  );
}

const FALLBACK_CONTACT = {
  company: "QuickKart Commerce Private Limited",
  email: "support@quickkart.local",
  phone: "1800-202-2026",
  whatsApp: "9999999999",
  address: "Plot 12, HITEC City, Hyderabad, Telangana 500081",
  hours: "Everyday, 7:00 AM – 11:00 PM",
  note: "We typically reply within a few minutes during store hours."
};

function normalizeContact(info) {
  if (!info) return { ...FALLBACK_CONTACT };
  return {
    company: info.company || info.Company || FALLBACK_CONTACT.company,
    email: info.email || info.Email || FALLBACK_CONTACT.email,
    phone: info.phone || info.Phone || FALLBACK_CONTACT.phone,
    whatsApp: info.whatsApp || info.WhatsApp || FALLBACK_CONTACT.whatsApp,
    address: info.address || info.Address || FALLBACK_CONTACT.address,
    hours: info.hours || info.Hours || FALLBACK_CONTACT.hours,
    note: info.note || info.Note || FALLBACK_CONTACT.note
  };
}

function loadStoredContact() {
  try {
    const raw = localStorage.getItem("qk_contact");
    return raw ? normalizeContact(JSON.parse(raw)) : null;
  } catch {
    return null;
  }
}

function Footer() {
  return (
    <footer className="sitefoot">
      <div className="footinner">
        <div>
          <strong className="footlogo">quickkart</strong>
          <p>Groceries and daily essentials, delivered in minutes.</p>
        </div>
        <div className="footlinks">
          <Link to="/">Home</Link>
          <Link to="/contact">Contact us</Link>
        </div>
      </div>
      <p className="copy">© QuickKart Commerce Private Limited, 2026-2027</p>
    </footer>
  );
}

function ContactPage() {
  const [info, setInfo] = useState(FALLBACK_CONTACT);
  useEffect(() => {
    const stored = loadStoredContact();
    if (stored) setInfo(stored);
    api.contact().then(row => {
      const next = normalizeContact(row);
      setInfo(next);
      localStorage.setItem("qk_contact", JSON.stringify(next));
    }).catch(() => setInfo(stored || FALLBACK_CONTACT));
  }, []);
  const phone = info.phone;
  const email = info.email;
  const whats = info.whatsApp;
  const hours = info.hours;
  const address = info.address;
  const company = info.company;
  const note = info.note;
  const wa = String(whats).replace(/\D/g, "");
  return (
    <section className="section contactpage">
      <div className="contacthero">
        <p>We’re here to help</p>
        <h1>Contact us</h1>
        <span>{note}</span>
      </div>
      <div className="contactgrid">
        <article>
          <h3>Call</h3>
          <a href={`tel:${phone}`}>{phone}</a>
        </article>
        <article>
          <h3>Email</h3>
          <a href={`mailto:${email}`}>{email}</a>
        </article>
        <article>
          <h3>WhatsApp</h3>
          <a href={wa ? `https://wa.me/91${wa.replace(/^91/, "")}` : "#"} target="_blank" rel="noreferrer">{whats}</a>
        </article>
        <article>
          <h3>Hours</h3>
          <p>{hours}</p>
        </article>
        <article className="wide">
          <h3>Registered office</h3>
          <p>{company}</p>
          <p>{address}</p>
        </article>
      </div>
    </section>
  );
}

export default function App() {
  const { user, setUser } = useAuth();
  const [cartCount, setCartCount] = useState(0);
  const [wishIds, setWishIds] = useState(localWishIds());
  const [authMode, setAuthMode] = useState(null);
  const [params] = useSearchParams();
  const [searchQ, setSearchQ] = useState(params.get("q") || "");
  useEffect(() => {
    const open = (e) => setAuthMode(e.detail || "login");
    window.addEventListener("qk-auth", open);
    return () => window.removeEventListener("qk-auth", open);
  }, []);
  const refreshCart = () => {
    if (!getUser()) { setCartCount(0); return; }
    api.cart().then(c => setCartCount(c.items.reduce((s, i) => s + i.quantity, 0))).catch(() => setCartCount(0));
  };
  const refreshWish = () => {
    if (!getUser()) { setWishIds(localWishIds()); return; }
    api.wishlist().then(items => {
      const ids = items.map(i => i.productId);
      localStorage.setItem("qk_wish", JSON.stringify(ids));
      setWishIds(ids);
    }).catch(() => setWishIds(localWishIds()));
  };
  useEffect(() => {
    refreshCart();
    refreshWish();
    const h = () => { refreshCart(); };
    const w = () => refreshWish();
    window.addEventListener("qk-cart", h);
    window.addEventListener("qk-wish", w);
    return () => { window.removeEventListener("qk-cart", h); window.removeEventListener("qk-wish", w); };
  }, [user]);
  return (
    <>
      <Header user={user} cartCount={cartCount} wishCount={wishIds.length} searchQ={searchQ} setSearchQ={setSearchQ} onAuthOpen={setAuthMode} onLogout={() => { clearSession(); setUser(null); setCartCount(0); setWishIds(localWishIds()); }} />
      {authMode && <AuthModal startMode={authMode} onAuth={setUser} onClose={() => setAuthMode(null)} />}
      <main className="page">
      <Routes>
        <Route path="/" element={<Home searchQ={searchQ} wishIds={wishIds} cartCount={cartCount} />} />
        <Route path="/dashboard" element={<Dashboard wishCount={wishIds.length} cartCount={cartCount} />} />
        <Route path="/product/:id" element={<ProductPage />} />
        <Route path="/login" element={<NeedLogin mode="login" />} />
        <Route path="/signup" element={<NeedLogin mode="register" />} />
        <Route path="/wishlist" element={<WishlistPage wishIds={wishIds} />} />
        <Route path="/cart" element={<CartPage />} />
        <Route path="/checkout" element={<CheckoutPage />} />
        <Route path="/orders" element={<OrdersPage />} />
        <Route path="/orders/:id" element={<OrderDetail />} />
        <Route path="/rider" element={<RiderPage />} />
        <Route path="/admin" element={<AdminPage />} />
        <Route path="/contact" element={<ContactPage />} />
      </Routes>
      </main>
      <Footer />
    </>
  );
}
