import React, {useCallback, useEffect, useMemo, useRef, useState} from 'react';
import {createRoot} from 'react-dom/client';
import {jsPDF} from 'jspdf';
import {
  Users, ShoppingCart, Package, Settings, Phone, ChevronRight, Plus, Search,
  MessageCircle, ArrowLeft, Pencil, Mail, Globe, Landmark, MapPin, Truck,
  RefreshCw, FileText, RotateCcw, X, Boxes, BadgeEuro, Wifi, WifiOff, Save,
  Send, Download, Eye
} from 'lucide-react';
import './styles.css';
import logo from './assets/repredisl-logo.png';
import {allCachedClients, cacheClient, cacheClients, clearClientCache, recentCachedClients, searchCachedClients, allCachedProducts, cacheProducts, clearProductCache} from './db';
import {API_URL, loadInitialClientsApi, searchClientsApi, syncCachedClientsApi, loadProductsApi, loadTarifasApi, createOrderApi} from './clientApi';

const SELLERS=[
  {code:1,name:'Axa',series:'V1'},{code:2,name:'Fábrica 2',series:'V2'},{code:3,name:'Fábrica 3',series:'V3'},
  {code:4,name:'Tel',series:'V4'},{code:5,name:'Mr',series:'V5'},{code:6,name:'Otros',series:'V6'},
  {code:7,name:'Arturo',series:'V7'},{code:8,name:'Coste',series:'V8'},{code:9,name:'Ro',series:'V9'},
  {code:10,name:'Sin clasificar',series:'VA'},{code:11,name:'Ca',series:'VB'},{code:12,name:'Eventos',series:'VC'},
  {code:13,name:'Jose',series:'VD'}
];

const PRODUCTS=[
 {code:'A001',name:'Coca Cola 33cl',price:1.12,box:24,defaultQty:24,stock:580},
 {code:'A002',name:'Cerveza Estrella 33cl',price:.84,box:24,defaultQty:24,stock:304},
 {code:'A003',name:'Agua 1,5L',price:.61,box:12,defaultQty:null,stock:920},
 {code:'A004',name:'Patatas 150g',price:1.35,box:20,defaultQty:20,stock:86},
 {code:'A005',name:'Zumos 200ml',price:.45,box:24,defaultQty:null,stock:410}
];

const HISTORY={
 '00002':[
  {code:'A001',date:'31/07/2026',lastQty:12,lastPrice:1.12},
  {code:'A003',date:'31/07/2026',lastQty:18,lastPrice:.61},
  {code:'A005',date:'20/07/2026',lastQty:12,lastPrice:.45}
 ],
 '00001':[
  {code:'A002',date:'04/08/2026',lastQty:48,lastPrice:.84},
  {code:'A004',date:'04/08/2026',lastQty:20,lastPrice:1.35}
 ]
};

function Header({online}){
  return <><header className="top"><img src={logo} alt="REPREDISL"/><span className="online"><i className={online?'dot on':'dot'}/>{online?'Online':'Offline'}</span></header><div className="rule"/></>;
}
function Bottom({tab,setTab}){
  const items=[['clients','Clientes',Users],['orders','Pedidos',ShoppingCart],['catalog','Catálogo',Package],['config','Config',Settings]];
  return <nav className="bottom">{items.map(([id,label,Icon])=><button key={id} onClick={()=>setTab(id)} className={tab===id?'active':''}><Icon/><span>{label}</span></button>)}</nav>;
}
function Layout({children,tab,setTab,online}){return <div className="phone"><Header online={online}/><main>{children}</main><Bottom tab={tab} setTab={setTab}/></div>}
function ExternalLink({href,children,className=''}){return <a className={className} href={href} target={href.startsWith('http')?'_blank':undefined} rel="noreferrer" onClick={e=>e.stopPropagation()}>{children}</a>}

function Clients({clients,onOpen,onNew,seller,onSearch,loading,error}){
 const [q,setQ]=useState('');
 useEffect(()=>{
   const text=q.trim();
   if(text.length<2){onSearch(text);return;}
   const timer=setTimeout(()=>onSearch(text),300);
   return ()=>clearTimeout(timer);
 },[q,onSearch]);
 const caption=q.trim()?`${clients.length} encontrados`:`${clients.length} disponibles`;
 return <section className="page"><div className="eyebrow">Vendedor {seller.name} · Serie {seller.series}</div><div className="titleRow"><h1>Clientes</h1><span className="pill">{caption}</span></div>
  <div className="search"><Search/><input value={q} onChange={e=>setQ(e.target.value)} placeholder="Buscar cliente..." autoComplete="off"/></div>
  <div className="hint">Al abrir se cargan 30 clientes. Busca por código, nombre o NIF para consultar más.</div>
  {loading&&<div className="hint">Consultando servidor…</div>}
  {error&&<div className="hint" style={{color:'#a22'}}>{error}</div>}
  {!q.trim()&&clients.length===0&&!loading&&<div className="empty">No hay clientes disponibles. Comprueba la conexión o pulsa Sincronizar en Config.</div>}
  <div className="list">{clients.map(c=><article className="clientRow" key={c.code} onClick={()=>onOpen(c)}><div className="grow"><h3>{c.commercial}</h3><div className="muted">{c.code} · {c.fiscal} · {c.nif}</div></div><div className="quick">{c.phone&&<ExternalLink href={`tel:${c.phone}`}><Phone/></ExternalLink>}{c.mobile&&<ExternalLink className="wa" href={`https://wa.me/34${c.mobile.replace(/\D/g,'')}`}><MessageCircle/></ExternalLink>}<ChevronRight/></div></article>)}</div>
  <button className="fab" onClick={onNew} title="Nuevo cliente"><Plus/></button></section>;
}

function ClientOrders({orders,client,onBack,onOpen,onNew,onClientDetail}){
 const filtered=orders.filter(o=>o.clientCode===client.code);
 return <section className="page">
  <button className="back" onClick={onBack}><ArrowLeft/> Volver</button>
  <div className="clientOrdersHead"><div><div className="eyebrow">Cliente {client.code}</div><h1>{client.commercial}</h1><div className="muted">{client.fiscal} · {client.nif}</div></div><button className="roundPlus" onClick={onNew} title="Nuevo pedido"><Plus/></button></div>
  <button className="outline clientDataButton" onClick={onClientDetail}><Pencil/> Ver / editar datos del cliente</button>
  <h2 className="ordersTitle">Pedidos del cliente</h2>
  <div className="list">{filtered.length?filtered.map(o=><article className="orderRow" onClick={()=>onOpen(o)} key={o.id}><div><h3>{o.series}/{o.number}</h3><span className="muted">{o.date}</span></div><div className="right"><b>{money(o.total)}</b><span className={`status ${o.status==='Enviado'?'sent':''}`}>{o.status}</span><ChevronRight/></div></article>):<div className="empty">Todavía no hay pedidos para este cliente.<br/>Pulsa + para crear el primero.</div>}</div>
 </section>;
}

function ClientDetail({client,onBack,onEdit,onNewOrder,onOrders}){return <section className="page detail"><button className="back" onClick={onBack}><ArrowLeft/> Volver</button><div className="heroCard"><div><div className="eyebrow">Ficha de cliente</div><h1>{client.commercial}</h1><div className="pillInline">Código {client.code}</div></div><div className="heroActions"><button className="outline" onClick={onOrders}><FileText/> Ver pedidos</button><button className="primary" onClick={onNewOrder}><ShoppingCart/> Nuevo pedido</button><button className="outline" onClick={onEdit}><Pencil/> Editar datos</button></div></div>
 <Info title="Datos fiscales" icon={FileText}><b>Razón social</b><span>{client.fiscal}</span><b>NIF / CIF</b><span>{client.nif}</span></Info>
 <Address title="Dirección fiscal" data={client.fiscalAddress}/><div className="info"><h3>Nombre comercial</h3><p>{client.commercial}</p></div><Address title="Dirección de reparto" data={client.deliveryAddress} delivery/>
 <Info title="Contacto" icon={Users}><b>Teléfono</b><ExternalLink href={`tel:${client.phone}`}>{client.phone}</ExternalLink><b>Móvil / WhatsApp</b><span><ExternalLink href={`tel:${client.mobile}`}>{client.mobile}</ExternalLink> · <ExternalLink className="waText" href={`https://wa.me/34${client.mobile.replace(/\D/g,'')}`}>WhatsApp</ExternalLink></span><b>Correo electrónico</b><ExternalLink href={`mailto:${client.email}`}>{client.email}</ExternalLink><b>Página web</b><ExternalLink href={client.website}>{client.website.replace(/^https?:\/\//,'')}</ExternalLink></Info>
 <Info title="Datos bancarios" icon={Landmark}><b>Banco</b><span>{client.bank}</span><b>IBAN</b><span>{client.iban}</span></Info>
 <Info title="Control y sincronización" icon={RefreshCw}><b>Usuario última actualización</b><span>{client.lastUser}</span><b>Fecha última actualización</b><span>{client.lastUpdate}</span><b>Estado</b><span>Sincronizado</span><b>Última sincronización</b><span>{client.lastSync}</span></Info>
 </section>}
function Info({title,icon:Icon,children}){return <div className="info"><h3>{Icon&&<Icon/>}{title}</h3><div className="gridInfo">{children}</div></div>}
function Address({title,data,delivery}){return <div className="info"><h3>{delivery?<Truck/>:<MapPin/>}{title}</h3><div className="address"><label>Dirección<strong>{data.address}</strong></label><label>Código postal<strong>{data.postal}</strong></label><label>Población<strong>{data.city}</strong></label><label>Provincia<strong>{data.province}</strong></label></div></div>}

function ClientForm({value,onCancel,onSave,isNew}){
 const [c,setC]=useState(JSON.parse(JSON.stringify(value)));
 const set=(k,v)=>setC({...c,[k]:v});
 const addr=(type,k,v)=>setC({...c,[type]:{...c[type],[k]:v}});
 return <section className="page"><button className="back" onClick={onCancel}><ArrowLeft/> Cancelar</button><h1>{isNew?'Nuevo cliente':'Editar cliente'}</h1><div className="formCard"><label>Código<input value={c.code} onChange={e=>set('code',e.target.value)} disabled={!isNew}/></label><label>Nombre comercial<input value={c.commercial} onChange={e=>set('commercial',e.target.value)}/></label><label>Razón social<input value={c.fiscal} onChange={e=>set('fiscal',e.target.value)}/></label><label>NIF / CIF<input value={c.nif} onChange={e=>set('nif',e.target.value)}/></label><h3>Dirección fiscal</h3>{['address','postal','city','province'].map(k=><label key={k}>{fieldLabel(k)}<input value={c.fiscalAddress[k]} onChange={e=>addr('fiscalAddress',k,e.target.value)}/></label>)}<h3>Dirección de reparto</h3>{['address','postal','city','province'].map(k=><label key={k}>{fieldLabel(k)}<input value={c.deliveryAddress[k]} onChange={e=>addr('deliveryAddress',k,e.target.value)}/></label>)}<h3>Contacto</h3><label>Teléfono<input value={c.phone} onChange={e=>set('phone',e.target.value)}/></label><label>Móvil<input value={c.mobile} onChange={e=>set('mobile',e.target.value)}/></label><label>Correo electrónico<input value={c.email} onChange={e=>set('email',e.target.value)}/></label><label>Página web<input value={c.website} onChange={e=>set('website',e.target.value)}/></label><h3>Datos bancarios</h3><label>Banco<input value={c.bank} onChange={e=>set('bank',e.target.value)}/></label><label>IBAN<input value={c.iban} onChange={e=>set('iban',e.target.value)}/></label><button className="primary wide" onClick={()=>onSave(c)}><Save/> Guardar cliente</button><div className="hint center">Los clientes pueden crearse y editarse. No existe opción de borrado.</div></div></section>;
}
const fieldLabel=k=>({address:'Dirección',postal:'Código postal',city:'Población',province:'Provincia'})[k];
const emptyClient=()=>({code:'',commercial:'',fiscal:'',nif:'',phone:'',mobile:'',email:'',website:'https://',fiscalAddress:{address:'',postal:'',city:'',province:''},deliveryAddress:{address:'',postal:'',city:'',province:''},bank:'',iban:'',lastUser:'',lastUpdate:'',lastSync:'Nunca'});

function OrdersList({orders,onOpen,onNew}){
 return <section className="page"><div className="titleRow"><div><div className="eyebrow">Todos los vendedores / cliente actual</div><h1>Pedidos</h1></div><button className="roundPlus" onClick={onNew} title="Nuevo pedido"><Plus/></button></div><div className="hint">Para crear un pedido, selecciona primero el cliente.</div><div className="list">{orders.length?orders.map(o=><article className="orderRow" onClick={()=>onOpen(o)} key={o.id}><div><h3>{o.series}/{o.number}</h3><span className="muted">{o.date} · {o.clientName}</span></div><div className="right"><b>{money(o.total)}</b><span className={`status ${o.status==='Enviado'?'sent':''}`}>{o.status}</span><ChevronRight/></div></article>):<div className="empty">No hay pedidos.</div>}</div></section>;
}

function OrderDetail({order,client,onBack,onPdf,onResend}){
 return <section className="page"><button className="back" onClick={onBack}><ArrowLeft/> Volver</button><div className="eyebrow">{order.sellerName || ''} · {order.series}</div><h1>Pedido {order.series}/{order.number}</h1><div className="info"><h3><Users/>Cliente</h3><p><b>{client?.commercial || order.clientName}</b></p><p className="muted">{client?.code || order.clientCode} · {client?.fiscal || ''} {client?.nif?`· ${client.nif}`:''}</p></div><div className="list orderLinesView">{(order.lines||[]).filter(l=>l.qty>0).map(l=><div className="orderViewLine" key={l.code}><div><b>{l.name}</b><span>{l.code} · Caja {l.box} uds</span></div><div><b>{l.qty} uds</b><span>{money(l.qty*l.price)}</span></div></div>)}{(!order.lines||order.lines.length===0)&&<div className="empty">Este pedido de ejemplo no conserva líneas.</div>}</div><div className="summary"><span>Total pedido</span><strong>{money(order.total)}</strong></div><div className="orderDetailActions"><button className="outline" onClick={onPdf}><Download/> Ver / descargar PDF</button><button className="primary" onClick={onResend}><Send/> Reenviar al cliente</button></div></section>;
}

function Catalog({products,onAdd,online,draft}){
 const [q,setQ]=useState('');
 const rows=products.filter(p=>`${p.code} ${p.name}`.toLowerCase().includes(q.toLowerCase()));
 return <section className="page"><div className="eyebrow">Catálogo {draft?`· Pedido ${draft.series}/${draft.number}`:''}</div><div className="titleRow"><h1>Productos</h1><span className="pill">{rows.length}</span></div><div className="search"><Search/><input placeholder="Buscar producto..." value={q} onChange={e=>setQ(e.target.value)}/></div>{!draft&&<div className="hint">Puedes consultar el catálogo. Para añadir artículos, abre primero un pedido.</div>}<div className="list">{rows.map(p=><article className="productRow" key={p.code}><div><h3>{p.name}</h3><div className="muted">{p.code}</div><div>Precio referencia: <b>{money(p.price)}</b> · Caja: <b>{p.box} uds</b></div>{online&&<div className="stock">Existencias: {p.stock}</div>}</div><button onClick={()=>onAdd(p)}>Añadir</button></article>)}</div></section>;
}
function AddModal({product,onClose,onAdd}){
 const [qty,setQty]=useState(product.defaultQty??'');
 return <div className="modalBg" onMouseDown={onClose}><div className="modal" onMouseDown={e=>e.stopPropagation()}><h2>{product.name}</h2><div className="muted">{product.code} · Precio {money(product.price)} · Caja {product.box} uds</div><label>Unidades<input autoFocus type="number" min="0" step="1" value={qty} onChange={e=>setQty(e.target.value)} placeholder="Unidades"/></label>{product.defaultQty&&<small>Unidades predeterminadas: {product.defaultQty}</small>}<div className="modalActions"><button onClick={onClose}>Cancelar</button><button className="primary" disabled={!Number(qty)} onClick={()=>onAdd(Number(qty))}>Aceptar</button></div></div></div>;
}

function OrderEdit({order,setOrder,client,online,onBack,onCatalog,onFinalize}){
 const loadHistory=()=>{
   const hist=HISTORY[client.code]||[];
   const historical=hist.map(h=>{const p=PRODUCTS.find(x=>x.code===h.code);return {...p,qty:0,lastQty:h.lastQty,lastDate:h.date,price:h.lastPrice};});
   const existingNew=order.lines.filter(l=>!historical.some(h=>h.code===l.code));
   setOrder({...order,lines:[...historical,...existingNew]});
 };
 const setQty=(code,qty)=>setOrder({...order,lines:order.lines.map(l=>l.code===code?{...l,qty:Number(qty)||0}:l)});
 const remove=code=>setOrder({...order,lines:order.lines.filter(l=>l.code!==code)});
 const updatePrice=code=>setOrder({...order,lines:order.lines.map(l=>l.code===code?{...l,price:PRODUCTS.find(p=>p.code===code)?.price??l.price}:l)});
 const active=order.lines.filter(l=>l.qty>0);
 const total=active.reduce((s,l)=>s+l.qty*l.price,0);
 return <section className="page orderEdit"><button className="back" onClick={onBack}><ArrowLeft/> Volver</button><div className="eyebrow">Vendedor {order.sellerName} · Serie {order.series}</div><h1>Pedido {order.series}/{order.number}</h1><div className="clientOrder"><div><span>Cliente {client.code}</span><h2>{client.commercial}</h2></div><button className="outline" onClick={loadHistory}>Cargar historial</button></div>
 <div className="sectionHead"><b>Líneas de pedido</b><button className="linkBtn" onClick={onCatalog}><Plus/> Añadir del catálogo</button></div>
 {order.lines.length===0?<div className="empty">Pedido vacío. Puedes cargar el historial o añadir productos desde el catálogo.</div>:order.lines.map(l=><article className="line" key={l.code}><div className="lineInfo"><h3>{l.name}</h3><div className="muted">{l.code}{l.lastDate&&` · Última compra: ${l.lastDate}`}</div><div>{l.lastQty!=null&&<>Últ. cantidad: <b>{l.lastQty} uds</b> · </>}Unid./caja: <b>{l.box} uds</b></div><div>Precio: <b>{money(l.price)}</b></div>{online&&<div className="onlineButtons"><button onClick={()=>alert(`Existencias de ${l.name}: ${l.stock} uds`)}><Boxes/> Comprobar existencias</button><button onClick={()=>updatePrice(l.code)}><BadgeEuro/> Actualizar precio</button></div>}</div><div className="qty"><input type="number" min="0" step="1" value={l.qty} onChange={e=>setQty(l.code,e.target.value)} aria-label={`Unidades de ${l.name}`}/>{l.lastQty!=null&&<button className="repeat" onClick={()=>setQty(l.code,l.lastQty)}><RotateCcw/> Mismas ({l.lastQty})</button>}<button className="remove" onClick={()=>remove(l.code)} title="Quitar del pedido"><X/></button></div></article>)}
 <div className="summary"><span>{active.length} productos · {active.reduce((s,l)=>s+l.qty,0)} uds</span><strong>{money(total)}</strong></div><button className="primary wide final" disabled={!active.length} onClick={()=>onFinalize(total)}><FileText/> Hacer pedido</button><small className="center">Genera el PDF del pedido y registra el pedido. El envío real por correo/servidor se conectará a la API.</small></section>;
}

function Config({seller,setSeller,online,setOnline,lastSync,onSyncCache,onClearCache,syncing,syncMessage}){
 return <section className="page"><div className="eyebrow">Configuración</div><h1>Configuración y Sync</h1><div className="info"><h3>Vendedor y serie</h3><label className="selectLabel">Vendedor<select value={seller.code} onChange={e=>setSeller(SELLERS.find(s=>s.code===Number(e.target.value)))}>{SELLERS.map(s=><option key={s.code} value={s.code}>{s.code} · {s.name}</option>)}</select></label><div className="configRow"><span>Serie de pedido</span><b>{seller.series}</b></div></div><div className="info"><h3>Clientes</h3><div className="configRow"><span>Carga</span><b>Bajo demanda</b></div><div className="configRow"><span>Caché</span><b>IndexedDB · máx. 300</b></div><div className="configRow"><span>Última consulta servidor</span><b>{lastSync||'Nunca'}</b></div><button className="primary wide" onClick={onSyncCache} disabled={syncing}><RefreshCw/> {syncing?'Sincronizando...':'Sincronizar clientes'}</button><button className="outline wide" onClick={onClearCache} disabled={syncing}><X/> Vaciar caché de clientes</button>{syncMessage&&<div className="hint center">{syncMessage}</div>}</div><div className="info"><h3>Conexión</h3><div className="configRow"><span>PostgREST</span><b>{API_URL}</b></div><div className="configRow"><span>Estado</span><b>{online?'Conectado':'Offline / caché'}</b></div><button className="outline wide" onClick={()=>setOnline(!online)}>{online?<><WifiOff/> Simular Offline</>:<><Wifi/> Volver Online</>}</button></div></section>;
}

function buildPdf(order,client,total){
 const doc=new jsPDF();
 doc.setFontSize(18);doc.text(`Pedido ${order.series}/${order.number}`,20,20);
 doc.setFontSize(11);doc.text(`Cliente: ${client.commercial} (${client.code})`,20,30);doc.text(`Razón social: ${client.fiscal} - NIF/CIF: ${client.nif}`,20,37);
 let y=50;
 (order.lines||[]).filter(l=>l.qty>0).forEach(l=>{doc.text(`${l.code}  ${l.name}`,20,y);doc.text(`${l.qty} uds x ${Number(l.price).toFixed(2)} = ${(l.qty*l.price).toFixed(2)} EUR`,115,y);y+=8;});
 doc.setFontSize(14);doc.text(`TOTAL: ${Number(total).toFixed(2)} EUR`,20,y+10);
 return doc;
}
function downloadPdf(order,client,total){const doc=buildPdf(order,client,total);doc.save(`Pedido_${order.series}_${order.number}_${client.code}.pdf`)}

function App(){
 const [tab,setTab]=useState('clients');
 const [online,setOnline]=useState(typeof navigator !== 'undefined' ? navigator.onLine : true);
 const [seller,setSeller]=useState(SELLERS[12]);
 const [clients,setClients]=useState([]);
 const [screen,setScreen]=useState('clients');
 const [client,setClient]=useState(null);
 const [editing,setEditing]=useState(null);
 const [lastSync,setLastSync]=useState('');
 const [clientSearchLoading,setClientSearchLoading]=useState(false);
 const [clientSearchError,setClientSearchError]=useState('');
 const [syncingClients,setSyncingClients]=useState(false);
 const [syncMessage,setSyncMessage]=useState('');
 const clientSearchAbort=useRef(null);
 const [orders,setOrders]=useState([
  {id:1,series:'VD',number:2987,date:'06/08/2026',clientCode:'00002',clientName:'Cafetería Sol',sellerName:'Jose',total:339.48,status:'Enviado',lines:[{...PRODUCTS[0],qty:24,price:1.12},{...PRODUCTS[2],qty:36,price:.61},{...PRODUCTS[4],qty:18,price:.45}]}
 ]);
 const [draft,setDraft]=useState(null);
 const [modalProduct,setModalProduct]=useState(null);
 const [viewOrder,setViewOrder]=useState(null);

 useEffect(()=>{
   const handleOnline=()=>setOnline(true);
   const handleOffline=()=>setOnline(false);
   window.addEventListener('online',handleOnline);
   window.addEventListener('offline',handleOffline);
   return ()=>{
     window.removeEventListener('online',handleOnline);
     window.removeEventListener('offline',handleOffline);
   };
 },[]);

 useEffect(()=>{
   let cancelled=false;
   const loadStartupClients=async()=>{
     setClientSearchLoading(true);
     setClientSearchError('');
     try{
       const cached=await recentCachedClients(30);
       if(!cancelled && cached.length) setClients(cached);

       const remote=await loadInitialClientsApi();
       await cacheClients(remote);
       if(!cancelled){
         setClients(remote);
         setOnline(true);
         setLastSync(new Date().toLocaleString('es-ES'));
       }
     }catch(err){
       console.error('Error cargando clientes iniciales:',err);
       if(!cancelled){
         setOnline(false);
         const cached=await recentCachedClients(30).catch(()=>[]);
         if(cached.length) setClients(cached);
         setClientSearchError(cached.length
           ? 'Sin conexión con la API. Se muestran clientes guardados en este navegador.'
           : 'No se ha podido cargar clientes desde la API.');
       }
     }finally{
       if(!cancelled) setClientSearchLoading(false);
     }
   };
   loadStartupClients();
   return ()=>{cancelled=true;};
 },[]);

 const searchClients=useCallback(async(text)=>{
   const q=String(text||'').trim();
   try{
     const cached=await searchCachedClients(q,30);
     setClients(cached);
   }catch(err){console.error('Error leyendo IndexedDB:',err);}

   if(q.length<2){
     clientSearchAbort.current?.abort();
     setClientSearchLoading(false);
     setClientSearchError('');
     return;
   }
   clientSearchAbort.current?.abort();
   const controller=new AbortController();
   clientSearchAbort.current=controller;
   setClientSearchLoading(true);
   setClientSearchError('');
   try{
     const remote=await searchClientsApi(q,{signal:controller.signal});
     await cacheClients(remote);
     if(!controller.signal.aborted) setClients(remote);
     setOnline(true);
     setLastSync(new Date().toLocaleString('es-ES'));
   }catch(err){
     if(err?.name==='AbortError') return;
     console.error('Error buscando clientes en PostgREST:',err);
     setOnline(false);
     setClientSearchError('No responde PostgREST. Se muestran los clientes disponibles en caché.');
   }finally{
     if(!controller.signal.aborted) setClientSearchLoading(false);
   }
 },[]);

 const clearClients=async()=>{
   await clearClientCache();
   setClients([]);
   setLastSync('');
   setClientSearchError('');
 };


 const syncCachedClients=async()=>{
   setSyncingClients(true);
   setSyncMessage('');
   try{
     const cached=await allCachedClients();

     const initial=await loadInitialClientsApi();
     let refreshed=[];
     if(cached.length){
       refreshed=await syncCachedClientsApi(cached.map(c=>c.code));
     }

     const merged=new Map();
     [...initial,...refreshed].forEach(c=>{if(c?.code) merged.set(c.code,c);});
     const updated=[...merged.values()];
     await cacheClients(updated);

     setClients(initial);
     const now=new Date().toLocaleString('es-ES');
     setLastSync(now);
     setOnline(true);
     setSyncMessage(cached.length
       ? `Actualizados ${initial.length} clientes iniciales y ${refreshed.length} clientes de la caché.`
       : `Cargados ${initial.length} clientes iniciales desde la API.`);
   }catch(err){
     console.error('Error sincronizando clientes en caché:',err);
     setOnline(false);
     setSyncMessage('No se ha podido sincronizar. Se mantiene la caché actual.');
   }finally{
     setSyncingClients(false);
   }
 };

 const saveClientLocal=async(saved)=>{
   await cacheClient(saved);
   setClients(current=>{
     const exists=current.some(x=>x.code===saved.code);
     return exists?current.map(x=>x.code===saved.code?saved:x):[saved,...current].slice(0,30);
   });
 };

 const openClientOrders=(c)=>{cacheClient(c).catch(console.error);setClient(c);setTab('clients');setScreen('clientOrders')};
 const newOrder=(c)=>{
   const nums=orders.filter(o=>o.series===seller.series).map(o=>o.number);
   const number=(nums.length?Math.max(...nums):2987)+1;
   setClient(c);setDraft({series:seller.series,number,sellerName:seller.name,clientCode:c.code,lines:[]});setTab('orders');setScreen('orderEdit');
 };
 const finalize = async (total) => {
    downloadPdf(draft, client, total);
    
    let orderStatus = online ? 'Enviado' : 'Pendiente sync';
    let serverOk = false;
    let serverError = '';

    const orderPayload = {
      id_cliente: Number(client.code) || 0,
      cliente: client.commercial || client.fiscal || `Cliente ${client.code}`,
      total: Number(total) || 0,
      serie: String(draft.series || '1'),
      numero_pedido: Number(draft.number) || 0,
      id_vendedor: Number(seller?.code) || 1,
      vendedor: seller?.name || '',
      id_forma_pago: Number(client.paymentMethodId) || 1,
      id_tarifa: Number(client.rateId) || 1,
      lineas: (draft.lines || []).map(l => ({
        code: l.code,
        desc: l.name || l.desc || l.description || '',
        qty: Number(l.qty) || 1,
        price: Number(l.price) || 0,
        subtotal: Number(((Number(l.qty) || 1) * (Number(l.price) || 0)).toFixed(2))
      }))
    };

    if (online) {
      try {
        await createOrderApi(orderPayload);
        serverOk = true;
      } catch (err) {
        console.warn('Error enviando pedido a la API:', err);
        orderStatus = 'Pendiente sync';
        serverError = err.message;
      }
    }

    const record = {
      id: Date.now(),
      series: draft.series,
      number: draft.number,
      date: new Date().toLocaleDateString('es-ES'),
      clientCode: client.code,
      clientName: client.commercial,
      sellerName: seller.name,
      total,
      status: orderStatus,
      lines: draft.lines.map(x => ({ ...x }))
    };

    setOrders([record, ...orders]);

    if (serverOk) {
      alert(`¡Pedido ${draft.series}/${draft.number} ENVIADO con éxito a la API y registrado en PostgreSQL!\nTotal: ${Number(total).toFixed(2)} €\nPDF descargado.`);
    } else if (serverError) {
      alert(`Pedido ${draft.series}/${draft.number} guardado localmente.\nAviso: No se pudo enviar al servidor (${serverError}). Queda pendiente de sincronización.\nPDF descargado.`);
    } else {
      alert(`Pedido ${draft.series}/${draft.number} guardado localmente (Modo Offline).\nQueda pendiente de sincronizar cuando haya red.\nPDF descargado.`);
    }

    setDraft(null);
    setTab('clients');
    setScreen('clientOrders');
  };
 const addToDraft=(p,qty)=>{
   if(!draft){alert('Primero crea un pedido desde un cliente.');setModalProduct(null);return;}
   const exists=draft.lines.find(l=>l.code===p.code);
   setDraft({...draft,lines:exists?draft.lines.map(l=>l.code===p.code?{...l,qty}:l):[...draft.lines,{...p,qty,price:p.price}]});
   setModalProduct(null);setTab('orders');setScreen('orderEdit');
 };
 const openOrder=(o)=>{setViewOrder(o);setClient(clients.find(c=>c.code===o.clientCode)||client);setScreen('orderDetail')};
 const resendOrder=(o,c)=>{downloadPdf(o,c,o.total);window.location.href=`mailto:${encodeURIComponent(c.email)}?subject=${encodeURIComponent(`Pedido ${o.series}/${o.number}`)}&body=${encodeURIComponent(`Adjunta el PDF del pedido ${o.series}/${o.number} que se acaba de generar.`)}`};

 let content;
 if(tab==='clients'){
   if(screen==='clientOrders'&&client) content=<ClientOrders orders={orders} client={client} onBack={()=>{setClient(null);setScreen('clients')}} onOpen={openOrder} onNew={()=>newOrder(client)} onClientDetail={()=>setScreen('clientDetail')}/>;
   else if(screen==='clientDetail'&&client) content=<ClientDetail client={client} onBack={()=>setScreen('clientOrders')} onEdit={()=>{setEditing(client);setScreen('clientForm')}} onNewOrder={()=>newOrder(client)} onOrders={()=>setScreen('clientOrders')}/>;
   else if(screen==='clientForm') content=<ClientForm value={editing||emptyClient()} isNew={!editing} onCancel={()=>setScreen(editing?'clientDetail':'clients')} onSave={async c=>{const now=new Date().toLocaleString('es-ES');const saved={...c,lastUser:seller.name,lastUpdate:now,lastSync:c.lastSync||'Nunca'};await saveClientLocal(saved);setClient(saved);setEditing(null);setScreen('clientOrders')}}/>;
   else if(screen==='orderDetail'&&viewOrder) {const c=clients.find(x=>x.code===viewOrder.clientCode);content=<OrderDetail order={viewOrder} client={c} onBack={()=>setScreen(client?'clientOrders':'clients')} onPdf={()=>downloadPdf(viewOrder,c,viewOrder.total)} onResend={()=>resendOrder(viewOrder,c)}/>;}
   else content=<Clients clients={clients} seller={seller} onOpen={openClientOrders} onNew={()=>{setEditing(null);setScreen('clientForm')}} onSearch={searchClients} loading={clientSearchLoading} error={clientSearchError}/>;
 } else if(tab==='orders'){
   if(screen==='orderEdit'&&draft&&client) content=<OrderEdit order={draft} setOrder={setDraft} client={client} online={online} onBack={()=>{setTab('clients');setScreen('clientOrders')}} onCatalog={()=>{setTab('catalog');setScreen('catalog')}} onFinalize={finalize}/>;
   else if(screen==='orderDetail'&&viewOrder) {const c=clients.find(x=>x.code===viewOrder.clientCode);content=<OrderDetail order={viewOrder} client={c} onBack={()=>setScreen('orders')} onPdf={()=>downloadPdf(viewOrder,c,viewOrder.total)} onResend={()=>resendOrder(viewOrder,c)}/>;}
   else content=<OrdersList orders={orders} onOpen={openOrder} onNew={()=>{setTab('clients');setScreen('clients')}}/>;
 } else if(tab==='catalog') content=<Catalog products={PRODUCTS} online={online} draft={draft} onAdd={p=>setModalProduct(p)}/>;
 else content=<Config seller={seller} setSeller={setSeller} online={online} setOnline={setOnline} lastSync={lastSync} onSyncCache={syncCachedClients} onClearCache={clearClients} syncing={syncingClients} syncMessage={syncMessage}/>;

 const changeTab=(t)=>{
   setTab(t);
   if(t==='clients') setScreen(client?'clientOrders':'clients');
   else if(t==='orders') setScreen(draft?'orderEdit':'orders');
   else if(t==='catalog') setScreen('catalog');
   else setScreen('config');
 };

 return <Layout tab={tab} setTab={changeTab} online={online}>{content}{modalProduct&&<AddModal product={modalProduct} onClose={()=>setModalProduct(null)} onAdd={qty=>addToDraft(modalProduct,qty)}/>}</Layout>;
}

const money=n=>new Intl.NumberFormat('es-ES',{style:'currency',currency:'EUR'}).format(n||0);
createRoot(document.getElementById('root')).render(<App/>);
