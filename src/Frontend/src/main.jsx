import React, {useCallback, useEffect, useMemo, useRef, useState} from 'react';
import {createRoot} from 'react-dom/client';
import {jsPDF} from 'jspdf';
import { buildOrderPdf, downloadOrderPdf } from './pdfOrder.js';
import {
  Users, ShoppingCart, Package, Settings, Phone, ChevronRight, Plus, Search,
  MessageCircle, ArrowLeft, Pencil, Mail, Globe, Landmark, MapPin, Truck,
  RefreshCw, FileText, RotateCcw, X, Boxes, BadgeEuro, Wifi, WifiOff, Save,
  Send, Download, Check, AlertCircle
} from 'lucide-react';
import './styles.css';
import logo from './assets/repredisl-logo.png';
import {
  allCachedClients, cacheClient, cacheClients, clearClientCache, recentCachedClients, searchCachedClients,
  allCachedProducts, cacheProducts, clearProductCache, updateCachedProduct,
  getConfigTerminal, saveConfigTerminal,
  savePendingOrder, getPendingOrders, getAllOrdersLocal, markOrderSynced
} from './db';
import {
  API_URL, loadInitialClientsApi, searchClientsApi, syncCachedClientsApi,
  loadProductsApi, fetchSingleProductApi, loadTarifasApi, loadVendedoresApi,
  loadHistorialVentasApi, getMaxOrderNumberApi, createOrderApi, loadOrdersApi
} from './clientApi';

const DEFAULT_TERMINAL_CONFIG = {
  sellerId: 13,
  sellerName: 'Jose',
  series: 'VD',
  rateId: 1,
  rateName: 'ESTANDAR',
  lastOrderNumber: 0,
  lastSync: ''
};

function Toast({msg, type, onClose}){
  useEffect(()=>{if(!msg)return;const t=setTimeout(onClose,3000);return()=>clearTimeout(t);},[msg,onClose]);
  if(!msg) return null;
  return <div className={`toast toast-${type||'ok'}`}>{msg}</div>;
}

function Header({online}){
  return <><header className="top"><img src={logo} alt="REPREDISL"/><span className="online"><i className={online?'dot on':'dot'}/>{online?'Online':'Offline'}</span></header><div className="rule"/></>;
}

function Bottom({tab,setTab}){
  const items=[['clients','Clientes',Users],['orders','Pedidos',ShoppingCart],['products','Productos',Package],['config','Config',Settings]];
  return <nav className="bottom">{items.map(([id,label,Icon])=><button key={id} onClick={()=>setTab(id)} className={(tab===id||(id==='products'&&tab==='catalog'))?'active':''}><Icon/><span>{label}</span></button>)}</nav>;
}

function Layout({children,tab,setTab,online}){return <div className="phone"><Header online={online}/><main>{children}</main><Bottom tab={tab} setTab={setTab}/></div>}
function ExternalLink({href,children,className=''}){return <a className={className} href={href} target={href.startsWith('http')?'_blank':undefined} rel="noreferrer" onClick={e=>e.stopPropagation()}>{children}</a>}

function Clients({clients,onOpen,onNew,terminalConfig,onSearch,loading,error}){
  const [q,setQ]=useState('');
  useEffect(()=>{
    const text=q.trim();
    if(text.length<2){onSearch(text);return;}
    const timer=setTimeout(()=>onSearch(text),300);
    return ()=>clearTimeout(timer);
  },[q,onSearch]);
  const caption=q.trim()?`${clients.length} encontrados`:`${clients.length} disponibles`;
  return <section className="page">
    <div className="eyebrow">Vendedor {terminalConfig.sellerName} · Serie {terminalConfig.series}</div>
    <div className="titleRow"><h1>Clientes</h1><span className="pill">{caption}</span></div>
    <div className="search"><Search/><input value={q} onChange={e=>setQ(e.target.value)} placeholder="Buscar cliente..." autoComplete="off"/></div>
    <div className="hint">Busca por código, nombre o NIF para consultar más.</div>
    {loading&&<div className="hint">Consultando servidor…</div>}
    {error&&<div className="hint" style={{color:'#a22'}}>{error}</div>}
    {!q.trim()&&clients.length===0&&!loading&&<div className="empty">No hay clientes disponibles. Comprueba la conexión o pulsa Sincronizar en Config.</div>}
    <div className="list">
      {clients.map(c=>(
        <article className="clientRow" key={c.code} onClick={()=>onOpen(c)}>
          <div className="grow">
            <h3>{c.commercial}</h3>
            <div className="muted">{c.code} · {c.fiscal} · {c.nif}</div>
          </div>
          <div className="quick">
            {c.phone&&<ExternalLink href={`tel:${c.phone}`}><Phone/></ExternalLink>}
            {c.mobile&&<ExternalLink className="wa" href={`https://wa.me/34${c.mobile.replace(/\D/g,'')}`}><MessageCircle/></ExternalLink>}
            <ChevronRight/>
          </div>
        </article>
      ))}
    </div>
    <button className="fab" onClick={onNew} title="Nuevo cliente"><Plus/></button>
  </section>;
}

function ClientOrders({orders,client,onBack,onOpen,onNew,onClientDetail}){
  const filtered=orders.filter(o=>String(o.clientCode)===String(client.code));
  return <section className="page">
    <button className="back" onClick={onBack}><ArrowLeft/> Volver</button>
    <div className="clientOrdersHead">
      <div>
        <div className="eyebrow">Cliente {client.code}</div>
        <h1>{client.commercial}</h1>
        <div className="muted">{client.fiscal} · {client.nif}</div>
      </div>
      <button className="roundPlus" onClick={onNew} title="Nuevo pedido"><Plus/></button>
    </div>
    <button className="outline clientDataButton" onClick={onClientDetail}><Pencil/> Ver / editar datos del cliente</button>
    <h2 className="ordersTitle">Pedidos del cliente</h2>
    <div className="list">
      {filtered.length?filtered.map(o=>{
        const isSync = o.isHistorical || o.readonly || o.status==='Sincronizado' || o.status==='Enviado' || o.estado==='sincronizado';
        return (
          <article className="orderRow" onClick={()=>onOpen(o)} key={o.id_local || o.id}>
            <div>
              <h3>{o.series}/{o.number}</h3>
              <span className="muted">{o.date}</span>
            </div>
            <div className="right">
              <b>{money(o.total)}</b>
              <span className={`status ${isSync?'sent':''}`}>
                {isSync ? 'Sincronizado' : 'Pendiente'}
              </span>
              <ChevronRight/>
            </div>
          </article>
        );
      }):<div className="empty">Todavía no hay pedidos para este cliente.<br/>Pulsa + para crear el primero.</div>}
    </div>
  </section>;
}

function ClientDetail({client,onBack,onEdit,onNewOrder,onOrders}){
  return <section className="page detail">
    <button className="back" onClick={onBack}><ArrowLeft/> Volver</button>
    <div className="heroCard">
      <div>
        <div className="eyebrow">Ficha de cliente</div>
        <h1>{client.commercial}</h1>
        <div className="pillInline">Código {client.code}</div>
      </div>
      <div className="heroActions">
        <button className="outline" onClick={onOrders}><FileText/> Ver pedidos</button>
        <button className="primary" onClick={onNewOrder}><ShoppingCart/> Nuevo pedido</button>
        <button className="outline" onClick={onEdit}><Pencil/> Editar datos</button>
      </div>
    </div>
    <Info title="Datos fiscales" icon={FileText}><b>Razón social</b><span>{client.fiscal}</span><b>NIF / CIF</b><span>{client.nif}</span></Info>
    <Address title="Dirección fiscal" data={client.fiscalAddress}/>
    <div className="info"><h3>Nombre comercial</h3><p>{client.commercial}</p></div>
    <Address title="Dirección de reparto" data={client.deliveryAddress} delivery/>
    <Info title="Contacto" icon={Users}>
      <b>Teléfono</b><ExternalLink href={`tel:${client.phone}`}>{client.phone}</ExternalLink>
      <b>Móvil / WhatsApp</b><span><ExternalLink href={`tel:${client.mobile}`}>{client.mobile}</ExternalLink> · <ExternalLink className="waText" href={`https://wa.me/34${client.mobile.replace(/\D/g,'')}`}>WhatsApp</ExternalLink></span>
      <b>Correo electrónico</b><ExternalLink href={`mailto:${client.email}`}>{client.email}</ExternalLink>
      <b>Página web</b><ExternalLink href={client.website}>{client.website.replace(/^https?:\/\//,'')}</ExternalLink>
    </Info>
    <Info title="Datos bancarios" icon={Landmark}><b>Banco</b><span>{client.bank}</span><b>IBAN</b><span>{client.iban}</span></Info>
    <Info title="Control y sincronización" icon={RefreshCw}>
      <b>Usuario última actualización</b><span>{client.lastUser}</span>
      <b>Fecha última actualización</b><span>{client.lastUpdate}</span>
      <b>Estado</b><span>Sincronizado</span>
      <b>Última sincronización</b><span>{client.lastSync}</span>
    </Info>
  </section>;
}

function Info({title,icon:Icon,children}){return <div className="info"><h3>{Icon&&<Icon/>}{title}</h3><div className="gridInfo">{children}</div></div>}
function Address({title,data,delivery}){return <div className="info"><h3>{delivery?<Truck/>:<MapPin/>}{title}</h3><div className="address"><label>Dirección<strong>{data.address}</strong></label><label>Código postal<strong>{data.postal}</strong></label><label>Población<strong>{data.city}</strong></label><label>Provincia<strong>{data.province}</strong></label></div></div>}

function ClientForm({value,onCancel,onSave,isNew}){
  const [c,setC]=useState(JSON.parse(JSON.stringify(value)));
  const set=(k,v)=>setC({...c,[k]:v});
  const addr=(type,k,v)=>setC({...c,[type]:{...c[type],[k]:v}});
  return <section className="page">
    <button className="back" onClick={onCancel}><ArrowLeft/> Cancelar</button>
    <h1>{isNew?'Nuevo cliente':'Editar cliente'}</h1>
    <div className="formCard">
      <label>Código<input value={c.code} onChange={e=>set('code',e.target.value)} disabled={!isNew}/></label>
      <label>Nombre comercial<input value={c.commercial} onChange={e=>set('commercial',e.target.value)}/></label>
      <label>Razón social<input value={c.fiscal} onChange={e=>set('fiscal',e.target.value)}/></label>
      <label>NIF / CIF<input value={c.nif} onChange={e=>set('nif',e.target.value)}/></label>
      <h3>Dirección fiscal</h3>
      {['address','postal','city','province'].map(k=><label key={k}>{fieldLabel(k)}<input value={c.fiscalAddress[k]} onChange={e=>addr('fiscalAddress',k,e.target.value)}/></label>)}
      <h3>Dirección de reparto</h3>
      {['address','postal','city','province'].map(k=><label key={k}>{fieldLabel(k)}<input value={c.deliveryAddress[k]} onChange={e=>addr('deliveryAddress',k,e.target.value)}/></label>)}
      <h3>Contacto</h3>
      <label>Teléfono<input value={c.phone} onChange={e=>set('phone',e.target.value)}/></label>
      <label>Móvil<input value={c.mobile} onChange={e=>set('mobile',e.target.value)}/></label>
      <label>Correo electrónico<input value={c.email} onChange={e=>set('email',e.target.value)}/></label>
      <label>Página web<input value={c.website} onChange={e=>set('website',e.target.value)}/></label>
      <h3>Datos bancarios</h3>
      <label>Banco<input value={c.bank} onChange={e=>set('bank',e.target.value)}/></label>
      <label>IBAN<input value={c.iban} onChange={e=>set('iban',e.target.value)}/></label>
      <button className="primary wide" onClick={()=>onSave(c)}><Save/> Guardar cliente</button>
      <div className="hint center">Los clientes pueden crearse y editarse. No existe opción de borrado.</div>
    </div>
  </section>;
}
const fieldLabel=k=>({address:'Dirección',postal:'Código postal',city:'Población',province:'Provincia'})[k];
const emptyClient=()=>({code:'',commercial:'',fiscal:'',nif:'',phone:'',mobile:'',email:'',website:'https://',fiscalAddress:{address:'',postal:'',city:'',province:''},deliveryAddress:{address:'',postal:'',city:'',province:''},bank:'',iban:'',lastUser:'',lastUpdate:'',lastSync:'Nunca'});

function OrdersList({orders,onOpen,onNew}){
  return <section className="page">
    <div className="titleRow">
      <div>
        <div className="eyebrow">Pedidos del comercial</div>
        <h1>Pedidos</h1>
      </div>
      <button className="roundPlus" onClick={onNew} title="Nuevo pedido"><Plus/></button>
    </div>
    <div className="hint">Para crear un pedido, selecciona primero el cliente.</div>
    <div className="list">
      {orders.length?orders.map(o=>{
        const isSync = o.isHistorical || o.readonly || o.status==='Sincronizado' || o.status==='Enviado' || o.estado==='sincronizado';
        return (
          <article className="orderRow" onClick={()=>onOpen(o)} key={o.id_local || o.id}>
            <div>
              <h3>{o.series}/{o.number}</h3>
              <span className="muted">{o.date} · {o.clientName}</span>
            </div>
            <div className="right">
              <b>{money(o.total)}</b>
              <span className={`status ${isSync?'sent':''}`}>
                {isSync ? 'Sincronizado' : 'Pendiente'}
              </span>
              <ChevronRight/>
            </div>
          </article>
        );
      }):<div className="empty">No hay pedidos registrados en el dispositivo.</div>}
    </div>
  </section>;
}

function OrderDetail({order,client,onBack,onPdf,onResend}){
  const isSync = order.isHistorical || order.readonly || order.status==='Sincronizado' || order.status==='Enviado' || order.estado==='sincronizado';
  return <section className="page">
    <button className="back" onClick={onBack}><ArrowLeft/> Volver</button>
    <div className="eyebrow">{order.sellerName || ''} · {order.series}</div>
    <h1>Pedido {order.series}/{order.number}</h1>
    {isSync && (
      <div className="pillInline" style={{background:'#e8f5e9',color:'#1b5e20',borderColor:'#a5d6a7',marginBottom:'14px',fontWeight:600}}>
        🔒 Pedido sincronizado con central (Histórico · Solo lectura)
      </div>
    )}
    <div className="info">
      <h3><Users/>Cliente</h3>
      <p><b>{client?.commercial || order.clientName}</b></p>
      <p className="muted">{client?.code || order.clientCode} · {client?.fiscal || ''} {client?.nif?`· ${client.nif}`:''}</p>
    </div>
    <div className="list orderLinesView">
      {(order.lines||[]).filter(l=>Number(l.qty)>0).map(l=>(
        <div className="orderViewLine" key={l.code}>
          <div>
            <b>{l.name}</b>
            <span>{l.code}{l.box?` · Caja ${l.box} uds`:''}</span>
          </div>
          <div>
            <b>{l.qty} uds</b>
            <span>{money(l.qty * (l.price != null ? l.price : (l.currentPrice || 0)))}</span>
          </div>
        </div>
      ))}
      {(!order.lines||order.lines.length===0)&&<div className="empty">Este pedido no contiene líneas.</div>}
    </div>
    <div className="summary">
      <span>Total pedido</span>
      <strong>{money(order.total)}</strong>
    </div>
    <div className="orderDetailActions">
      <button className="outline" onClick={onPdf}><Download/> Ver / descargar PDF</button>
      <button className="primary" onClick={onResend}><Send/> Reenviar al cliente</button>
    </div>
  </section>;
}

function ProductsScreen({products,loading,onAdd,online,draft,rateName,rateId,onReload}){
  const [q,setQ]=useState('');
  const rows=useMemo(()=>{
    const term=q.trim().toLowerCase();
    if(!term) return products;
    return products.filter(p=>`${p.code || ''} ${p.name || ''}`.toLowerCase().includes(term));
  },[products,q]);

  return <section className="page">
    <div className="eyebrow">Tarifa: <b>{rateName || 'Estándar'}</b> (ID: {rateId || 1}) {draft?`· Pedido en curso ${draft.series}/${draft.number}`:''}</div>
    <div className="titleRow">
      <h1>Productos</h1>
      <span className="pill">{loading ? 'Cargando...' : `${rows.length} artículos`}</span>
    </div>
    <div className="search">
      <Search/>
      <input
        placeholder="Buscar producto por nombre o código..."
        value={q}
        onChange={e=>setQ(e.target.value)}
        autoComplete="off"
      />
    </div>

    {loading && <div className="hint" style={{color:'#2563eb'}}>Cargando catálogo con precios de la tarifa {rateName}...</div>}
    {!draft && (
      <div className="hint">
        Lista de productos y precios según tarifa <b>{rateName}</b>. Para añadir artículos a un pedido, abre primero el pedido en Clientes.
      </div>
    )}

    {!loading && rows.length===0 && (
      <div className="empty">
        <p>No hay productos disponibles para la tarifa <b>{rateName}</b>.</p>
        {online && (
          <button className="outline" style={{marginTop:'0.75rem'}} onClick={onReload}>
            <RefreshCw size={14}/> Recargar productos ahora
          </button>
        )}
      </div>
    )}

    <div className="list">
      {rows.map(p=>(
        <article className="productRow" key={p.code}>
          <div className="productInfo">
            <h3>{p.name}</h3>
            <div className="muted">Ref: {p.code}</div>
            <div className="productPriceRow">
              <span className="productPriceTag">
                Precio: <b>{p.price != null ? money(p.price) : 'No disponible'}</b>
              </span>
              {p.box != null && <span className="productBoxTag"> · Caja: <b>{p.box} uds</b></span>}
            </div>
            {p.stock != null ? (
              <div className="stock">Existencias: <b>{p.stock} uds</b></div>
            ) : (
              <div className="muted" style={{fontSize:'0.78rem'}}>Existencias: Sin datos</div>
            )}
          </div>
          {draft && (
            <button className="primary addBtn" onClick={()=>onAdd(p)}>
              + Añadir
            </button>
          )}
        </article>
      ))}
    </div>
  </section>;
}

function AddModal({product,onClose,onAdd}){
  const [qty,setQty]=useState(product.defaultQty || (product.box ? String(product.box) : '1'));
  return <div className="modalBg" onMouseDown={onClose}>
    <div className="modal" onMouseDown={e=>e.stopPropagation()}>
      <h2>{product.name}</h2>
      <div className="muted">
        {product.code} · Precio {product.price != null ? money(product.price) : 'No disponible'}
        {product.box ? ` · Caja ${product.box} uds` : ''}
      </div>
      <label>
        Unidades
        <input autoFocus type="number" min="1" step="1" value={qty} onChange={e=>setQty(e.target.value)} placeholder="Unidades"/>
      </label>
      <div className="modalActions">
        <button onClick={onClose}>Cancelar</button>
        <button className="primary" disabled={!Number(qty) || Number(qty) <= 0} onClick={()=>onAdd(Number(qty))}>Aceptar</button>
      </div>
    </div>
  </div>;
}

function OrderEdit({order,setOrder,client,online,onBack,onProducts,onCatalog,onFinalize,products,terminalConfig,showToast}){
  const [loadingHistory,setLoadingHistory]=useState(false);
  const handleGoToProducts = onProducts || onCatalog;

  const loadHistory=async()=>{
    setLoadingHistory(true);
    try{
      const history=await loadHistorialVentasApi(client.code);
      if(!history || history.length===0){
        showToast('Este cliente no tiene historial de compras disponible','warn');
        return;
      }
      
      const templateLines=history.map(h=>{
        const prodMatch=products.find(p=>p.code===h.productoCode);
        const currentPrice=prodMatch?.price ?? null;
        return {
          code: h.productoCode,
          name: h.nombreProducto || prodMatch?.name || `Producto ${h.productoCode}`,
          qty: h.cantidad, // La cantidad inicial es exactamente la última cantidad comprada
          currentPrice: currentPrice,
          price: currentPrice,
          lastPrice: h.precio,
          lastDate: h.fecha ? new Date(h.fecha).toLocaleDateString('es-ES') : '',
          lastQty: h.cantidad,
          box: prodMatch?.box ?? null,
          stock: prodMatch?.stock ?? null,
          validated: false // Cada línea de la plantilla empieza sin validar
        };
      });

      const existingNew=(order.lines||[]).filter(l=>!templateLines.some(t=>t.code===l.code));
      setOrder({...order,lines:[...templateLines,...existingNew]});
      showToast(`Cargadas ${templateLines.length} líneas de la última compra como plantilla`,'ok');
    }catch(err){
      console.error('Error cargando historial de compras:',err);
      showToast('Error cargando historial de compras desde el servidor','warn');
    }finally{
      setLoadingHistory(false);
    }
  };

  const handleSetQty=(code,newQtyVal)=>{
    const parsed=Number(newQtyVal);
    setOrder({
      ...order,
      lines: order.lines.map(l=>{
        if(l.code!==code) return l;
        return {
          ...l,
          qty: isNaN(parsed) ? 0 : parsed,
          validated: false
        };
      })
    });
  };

  const handleValidateLine=(code)=>{
    setOrder({
      ...order,
      lines: order.lines.map(l=>{
        if(l.code!==code) return l;
        return {...l, validated: true};
      })
    });
  };

  const handleRemoveLine=(code)=>{
    setOrder({
      ...order,
      lines: order.lines.filter(l=>l.code!==code)
    });
  };

  // Botón: Actualizar existencias desde central
  const handleRefreshStock=async(code)=>{
    if(!online){
      showToast('Sin conexión para consultar existencias en central','warn');
      return;
    }
    try{
      const fresh=await fetchSingleProductApi(code, terminalConfig.rateId);
      if(fresh){
        setOrder({
          ...order,
          lines: order.lines.map(l=>l.code===code?{...l, stock: fresh.stock}:l)
        });
        await updateCachedProduct(code, { stock: fresh.stock });
        showToast(`Existencias de ${fresh.name || code}: ${fresh.stock != null ? fresh.stock + ' uds' : 'Sin datos'}`,'ok');
      }else{
        showToast('Producto no encontrado en central','warn');
      }
    }catch(err){
      console.error('Error actualizando existencias:',err);
      showToast('Error consultando existencias en servidor','warn');
    }
  };

  // Botón: Actualizar precio desde central
  const handleRefreshPrice=async(code)=>{
    if(!online){
      showToast('Sin conexión para consultar precio en central','warn');
      return;
    }
    try{
      const fresh=await fetchSingleProductApi(code, terminalConfig.rateId);
      if(fresh && fresh.price != null){
        setOrder({
          ...order,
          lines: order.lines.map(l=>l.code===code?{...l, currentPrice: fresh.price, price: fresh.price}:l)
        });
        await updateCachedProduct(code, { price: fresh.price });
        showToast(`Precio actualizado para ${fresh.name || code}: ${money(fresh.price)}`,'ok');
      }else{
        showToast('Precio no disponible en central para esta tarifa','warn');
      }
    }catch(err){
      console.error('Error actualizando precio:',err);
      showToast('Error consultando precio en servidor','warn');
    }
  };

  // Solo las líneas validadas con cantidad > 0 entran en el cálculo
  const validatedLines=(order.lines||[]).filter(l=>l.validated===true && Number(l.qty)>0);
  const total=validatedLines.reduce((s,l)=>s+(Number(l.qty)*Number(l.currentPrice!=null?l.currentPrice:(l.price||0))),0);

  return <section className="page orderEdit">
    <button className="back" onClick={onBack}><ArrowLeft/> Volver</button>
    <div className="eyebrow">Vendedor {terminalConfig.sellerName} · Serie {order.series} · Tarifa {terminalConfig.rateName}</div>
    <h1>Pedido {order.series}/{order.number}</h1>
    <div className="clientOrder">
      <div>
        <span>Cliente {client.code}</span>
        <h2>{client.commercial}</h2>
      </div>
      <button className="outline" onClick={loadHistory} disabled={loadingHistory}>
        {loadingHistory ? 'Cargando...' : 'Cargar historial'}
      </button>
    </div>

    <div className="sectionHead">
      <b>Líneas de pedido ({order.lines?.length || 0})</b>
      <button className="linkBtn" onClick={handleGoToProducts}><Plus/> Añadir producto</button>
    </div>

    {(!order.lines || order.lines.length===0) ? (
      <div className="empty">
        Pedido vacío. Puedes pulsar <b>Cargar historial</b> para crear la plantilla con la última compra o pulsar <b>Añadir producto</b>.
      </div>
    ) : (
      order.lines.map(l=>(
        <article className={`orderLineCard ${l.validated?'line-validated':'line-pending'}`} key={l.code}>
          <div className="lineMainRow">
            <div className="lineInfoCol">
              <h3 className="lineProdName">{l.name}</h3>
              <div className="lineProdCode">{l.code}</div>
              <div className="linePriceRow">
                Precio referencia: <b>{l.currentPrice != null ? money(l.currentPrice) : (l.lastPrice != null ? money(l.lastPrice) : 'No disponible')}</b>
                {l.box != null ? <> · Caja: <b>{l.box} uds</b></> : null}
              </div>
              {l.stock != null ? (
                <div className="lineStockRow">Existencias: {l.stock}</div>
              ) : (
                <div className="lineStockRow">Existencias: Sin datos</div>
              )}
              {l.lastQty != null && (
                <div className="lineHistorySub">
                  Última compra: {l.lastQty} uds{l.lastPrice != null ? ` · ${money(l.lastPrice)}` : ''}{l.lastDate ? ` · ${l.lastDate}` : ''}
                </div>
              )}
              {online && (
                <div className="onlineButtons">
                  <button type="button" onClick={()=>handleRefreshStock(l.code)}>
                    <Boxes size={13}/> Stock
                  </button>
                  <button type="button" onClick={()=>handleRefreshPrice(l.code)}>
                    <BadgeEuro size={13}/> Precio
                  </button>
                </div>
              )}
            </div>

            <div className="lineControlCol">
              <div className="lineQtyPill">
                <input
                  className="qtyInputPill"
                  type="number"
                  min="0"
                  step="1"
                  value={l.qty}
                  onChange={e=>handleSetQty(l.code,e.target.value)}
                  aria-label={`Unidades de ${l.name}`}
                />
                <span className="qtyUdText">ud</span>
              </div>
              <div className="lineBtnPillGroup">
                <button
                  type="button"
                  className={`btnPillOk ${l.validated?'validated':''}`}
                  onClick={()=>handleValidateLine(l.code)}
                  title={l.validated ? 'Línea confirmada' : 'Confirmar línea'}
                >
                  OK
                </button>
                <button
                  type="button"
                  className="btnPillDelete"
                  onClick={()=>handleRemoveLine(l.code)}
                  title="Eliminar producto del pedido"
                >
                  X
                </button>
              </div>
            </div>
          </div>
        </article>
      ))
    )}

    <div className="summary">
      <span>
        {validatedLines.length} productos validados · {validatedLines.reduce((s,l)=>s+Number(l.qty),0)} uds
      </span>
      <strong>{money(total)}</strong>
    </div>

    <button
      className="primary wide final"
      disabled={validatedLines.length===0}
      onClick={()=>onFinalize(total)}
    >
      <FileText/> Hacer pedido
    </button>
    <small className="center">
      Al pulsar "Hacer pedido", solo se enviarán y guardarán las líneas validadas con [OK] y cantidad mayor que cero.
    </small>
  </section>;
}

function Config({
  terminalConfig,
  setTerminalConfig,
  online,
  setOnline,
  sellers,
  rates,
  pendingCount,
  onSyncAll,
  onClearClients,
  syncing,
  syncMessage,
  onTariffChange,
  onSeriesChange,
  onSellerChange,
  onRecalculateOrderNumber
}){
  const [editingSeries, setEditingSeries] = useState(terminalConfig.series || 'VD');

  useEffect(()=>{
    setEditingSeries(terminalConfig.series || 'VD');
  }, [terminalConfig.series]);

  const handleSellerSelect=(e)=>{
    const id=Number(e.target.value);
    if(onSellerChange){
      onSellerChange(id);
    }
  };

  const handleRateChange=(e)=>{
    const id=Number(e.target.value);
    const found=rates.find(r=>r.id===id);
    if(found){
      onTariffChange(found.id, found.name);
    }
  };

  const handleSeriesBlur=()=>{
    const val=editingSeries.trim().toUpperCase();
    if(val && val !== terminalConfig.series){
      if(onSeriesChange) onSeriesChange(val);
    }
  };

  const handleSeriesKeyDown=(e)=>{
    if(e.key==='Enter'){
      e.target.blur();
    }
  };

  return <section className="page">
    <div className="eyebrow">Configuración del Terminal</div>
    <h1>Terminal y Sincronización</h1>

    {pendingCount > 0 && (
      <div className="info" style={{borderColor:'#ffa8a8',background:'#fff5f5'}}>
        <h3 style={{color:'#c92a2a'}}><AlertCircle/> Pedidos pendientes de sincronizar</h3>
        <p>Hay <b>{pendingCount} pedido(s)</b> pendientes de enviar al servidor.</p>
      </div>
    )}

    <div className="info">
      <h3>Vendedor y Serie</h3>
      <label className="selectLabel">
        Vendedor
        <select value={terminalConfig.sellerId} onChange={handleSellerSelect}>
          {sellers.map(s=>(
            <option key={s.code} value={s.code}>{s.code} · {s.name} ({s.series})</option>
          ))}
        </select>
      </label>
      <label className="selectLabel">
        Serie de facturación / pedidos
        <div style={{display:'flex',gap:'8px',alignItems:'center',marginTop:'4px'}}>
          <input
            type="text"
            value={editingSeries}
            onChange={e=>setEditingSeries(e.target.value.toUpperCase())}
            onBlur={handleSeriesBlur}
            onKeyDown={handleSeriesKeyDown}
            maxLength={4}
            style={{border:'1px solid #ccc',borderRadius:8,padding:'8px 12px',fontWeight:700,fontSize:16,width:'100px'}}
          />
          <button
            type="button"
            className="outline"
            style={{padding:'8px 12px',fontSize:'13px',borderRadius:8}}
            onClick={()=>{
              const val=editingSeries.trim().toUpperCase();
              if(val && onSeriesChange) onSeriesChange(val);
            }}
            title="Recalcular último pedido para esta serie"
          >
            <RefreshCw size={13}/> Recalcular n.º
          </button>
        </div>
      </label>
      <div className="hint" style={{margin:'4px 0'}}>
        Al cambiar la serie o el vendedor, se recalcula automáticamente el número del último pedido con el servidor y pedidos locales.
      </div>
    </div>

    <div className="info">
      <h3>Tarifa de Precios</h3>
      <label className="selectLabel">
        Tarifa de catálogo
        <select value={terminalConfig.rateId} onChange={handleRateChange}>
          {rates.map(r=>(
            <option key={r.id} value={r.id}>{r.id} · {r.name}</option>
          ))}
        </select>
      </label>
      <div className="hint" style={{margin:'4px 0'}}>
        Al cambiar de tarifa, se reemplaza la caché de productos para evitar mezclar precios.
      </div>
    </div>

    <div className="info">
      <h3>Estado y Sincronización</h3>
      <div className="configRow"><span>Conexión central</span><b>{online ? 'Online (Conectado)' : 'Offline (Sin red)'}</b></div>
      <div className="configRow">
        <span>Último pedido de la serie ({terminalConfig.series})</span>
        <div style={{display:'flex',gap:'8px',alignItems:'center'}}>
          <b>N.º {terminalConfig.lastOrderNumber || 0}</b>
          <button
            type="button"
            className="outline"
            style={{padding:'3px 8px',fontSize:'12px',borderRadius:'6px'}}
            onClick={()=>{
              if(onRecalculateOrderNumber) onRecalculateOrderNumber(terminalConfig.series);
            }}
            title="Recalcular número ahora"
          >
            <RefreshCw size={11}/>
          </button>
        </div>
      </div>
      <div className="configRow"><span>Pedidos pendientes offline</span><b className={pendingCount>0?'badge-pending-sync':''}>{pendingCount}</b></div>
      <div className="configRow"><span>Última sincronización</span><b>{terminalConfig.lastSync || 'Pendiente'}</b></div>
      
      <button className="primary wide" style={{marginTop:12}} onClick={onSyncAll} disabled={syncing}>
        <RefreshCw className={syncing?'spin':''}/> {syncing ? 'Sincronizando...' : 'Sincronizar ahora'}
      </button>
      <button className="outline wide" style={{marginTop:8}} onClick={onClearClients} disabled={syncing}>
        <X/> Vaciar caché de clientes
      </button>
      {syncMessage && <div className="hint center" style={{marginTop:10}}>{syncMessage}</div>}
    </div>

    <div className="info">
      <h3>Simulación de Red</h3>
      <button className="outline wide" onClick={()=>setOnline(!online)}>
        {online ? <><WifiOff/> Simular Modo Offline</> : <><Wifi/> Reconectar Modo Online</>}
      </button>
    </div>
  </section>;
}

function buildPdf(order,client,total,terminalConfig){
  return buildOrderPdf(order,client,total,terminalConfig);
}
function downloadPdf(order,client,total,terminalConfig){
  return downloadOrderPdf(order,client,total,terminalConfig);
}

function App(){
  const [tab,setTab]=useState('clients');
  const [online,setOnline]=useState(typeof navigator !== 'undefined' ? navigator.onLine : true);
  const [terminalConfig,setTerminalConfig]=useState(DEFAULT_TERMINAL_CONFIG);
  const [sellers,setSellers]=useState([{code:13,name:'Jose',series:'VD'}]);
  const [rates,setRates]=useState([{id:1,name:'ESTANDAR'}]);
  const [products,setProducts]=useState([]);
  const [loadingProducts,setLoadingProducts]=useState(false);
  const [clients,setClients]=useState([]);
  const [screen,setScreen]=useState('clients');
  const [client,setClient]=useState(null);
  const [editing,setEditing]=useState(null);
  const [clientSearchLoading,setClientSearchLoading]=useState(false);
  const [clientSearchError,setClientSearchError]=useState('');
  const [syncing,setSyncing]=useState(false);
  const [syncMessage,setSyncMessage]=useState('');
  const [orders,setOrders]=useState([]);
  const [draft,setDraft]=useState(null);
  const [modalProduct,setModalProduct]=useState(null);
  const [viewOrder,setViewOrder]=useState(null);
  const [toast,setToast]=useState({msg:'',type:'ok'});
  const clientSearchAbort=useRef(null);

  const showToast=useCallback((msg,type='ok')=>setToast({msg,type}),[]);
  const hideToast=useCallback(()=>setToast({msg:'',type:'ok'}),[]);

  const reloadProducts=useCallback(async(forcedRateId)=>{
    const rId = forcedRateId || terminalConfig.rateId || 1;
    setLoadingProducts(true);
    try{
      const remote = await loadProductsApi(rId);
      if(remote && remote.length){
        await clearProductCache();
        await cacheProducts(remote, rId);
        setProducts(remote);
      }
    }catch(err){
      console.warn('Error cargando productos:', err);
    }finally{
      setLoadingProducts(false);
    }
  },[terminalConfig.rateId]);

  useEffect(()=>{
    if((tab==='products' || tab==='catalog') && products.length===0 && !loadingProducts){
      reloadProducts();
    }
  },[tab, products.length, loadingProducts, reloadProducts]);

  // 1. Detección online / offline
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

  // 2. Carga inicial de Configuración del Terminal e IndexedDB
  useEffect(()=>{
    let cancelled=false;
    const initTerminal=async()=>{
      try{
        const savedCfg=await getConfigTerminal();
        const activeCfg=savedCfg ? { ...DEFAULT_TERMINAL_CONFIG, ...savedCfg } : DEFAULT_TERMINAL_CONFIG;
        if(!cancelled) setTerminalConfig(activeCfg);

        // Cargar productos en caché para la tarifa activa
        const cachedProds=await allCachedProducts(activeCfg.rateId);
        if(!cancelled && cachedProds.length) setProducts(cachedProds);

        // Cargar pedidos locales previos (pendientes e históricos)
        const localOrders=await getAllOrdersLocal().catch(()=>[]);
        if(!cancelled && localOrders.length){
          setOrders(localOrders);
        }

        // Si hay conexión, descargar datos maestros, pedidos históricos y consultar MAX(numero_pedido)
        if(navigator.onLine){
          try{
            const [remoteSellers, remoteRates, maxOrder, remoteOrders] = await Promise.all([
              loadVendedoresApi().catch(()=>[]),
              loadTarifasApi().catch(()=>[]),
              getMaxOrderNumberApi(activeCfg.series).catch(()=>0),
              loadOrdersApi(activeCfg.sellerId).catch(()=>[])
            ]);

            if(!cancelled){
              if(remoteSellers.length) setSellers(remoteSellers);
              if(remoteRates.length) setRates(remoteRates);

              if(remoteOrders.length){
                setOrders(current=>{
                  const map=new Map();
                  remoteOrders.forEach(ro=>{
                    map.set(`${ro.series}_${ro.number}`, ro);
                  });
                  (current||[]).forEach(lo=>{
                    const key=`${lo.series || lo.serie}_${lo.number || lo.numero_pedido}`;
                    if(!map.has(key) || lo.status==='Pendiente sync' || lo.estado==='pendiente'){
                      map.set(key, lo);
                    }
                  });
                  return Array.from(map.values());
                });
              }

              // Si el servidor tiene un número mayor al local, actualizarlo
              const finalMaxOrder = Math.max(activeCfg.lastOrderNumber || 0, maxOrder || 0);
              const updatedCfg = {
                ...activeCfg,
                lastOrderNumber: finalMaxOrder,
                lastSync: new Date().toLocaleString('es-ES')
              };
              setTerminalConfig(updatedCfg);
              await saveConfigTerminal(updatedCfg);
            }

            // Descargar productos reales de la tarifa
            const remoteProducts=await loadProductsApi(activeCfg.rateId);
            if(!cancelled && remoteProducts.length){
              await cacheProducts(remoteProducts, activeCfg.rateId);
              setProducts(remoteProducts);
            }
          }catch(netErr){
            console.warn('Iniciando en modo local por fallo de red:', netErr);
          }
        }
      }catch(err){
        console.error('Error inicializando terminal:', err);
      }
    };
    initTerminal();
    return ()=>{cancelled=true;};
  }, []);

  // 3. Carga inicial de clientes
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

  // 4. Búsqueda de clientes
  const searchClients=useCallback(async(text)=>{
    const q=String(text||'').trim();
    try{
      const cached=await searchCachedClients(q,30);
      setClients(cached);
    }catch(err){console.error('Error leyendo clientes en IndexedDB:',err);}

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
    setClientSearchError('');
    showToast('Caché de clientes vaciada','ok');
  };

  // 5. Cambio de Tarifa: borra caché anterior y descarga productos de la nueva tarifa
  const handleTariffChange=async(newRateId, newRateName)=>{
    try{
      await clearProductCache();
      const updatedCfg={
        ...terminalConfig,
        rateId: newRateId,
        rateName: newRateName
      };
      setTerminalConfig(updatedCfg);
      await saveConfigTerminal(updatedCfg);

      showToast(`Tarifa cambiada a ${newRateName}. Descargando productos...`,'ok');
      const remoteProds=await loadProductsApi(newRateId);
      await cacheProducts(remoteProds, newRateId);
      setProducts(remoteProds);
      showToast(`Descargados ${remoteProds.length} productos con tarifa ${newRateName}`,'ok');
    }catch(err){
      console.error('Error cambiando de tarifa:',err);
      showToast('Error cargando productos de la nueva tarifa','warn');
    }
  };

  // 6. Recalcular número de último pedido por serie
  const recalculateLastOrderNumber=useCallback(async(targetSeries)=>{
    const s=String(targetSeries || terminalConfig.series || 'VD').trim().toUpperCase();
    if(!s) return 0;
    try{
      const serverMax=await getMaxOrderNumberApi(s);
      const localPending=await getPendingOrders().catch(()=>[]);
      const localOrdersSeries=localPending.filter(p=>String(p.serie || p.series || '').toUpperCase()===s);
      const localMax=localOrdersSeries.reduce((max,p)=>Math.max(max,Number(p.numero_pedido || p.number)||0),0);
      const newMax=Math.max(serverMax||0, localMax||0);

      setTerminalConfig(prev=>{
        const updated={
          ...prev,
          series: s,
          lastOrderNumber: newMax
        };
        saveConfigTerminal(updated).catch(console.error);
        return updated;
      });

      showToast(`Serie ${s}: último pedido recalculado (N.º ${newMax})`,'ok');
      return newMax;
    }catch(err){
      console.error('Error recalculando último pedido para serie:',s,err);
      showToast(`No se pudo consultar el servidor para la serie ${s}`,'warn');
      return terminalConfig.lastOrderNumber || 0;
    }
  },[terminalConfig.series, terminalConfig.lastOrderNumber, showToast]);

  const handleSeriesChange=useCallback(async(newSeries)=>{
    const cleanSeries=String(newSeries||'').trim().toUpperCase();
    if(!cleanSeries) return;
    await recalculateLastOrderNumber(cleanSeries);
  },[recalculateLastOrderNumber]);

  const handleSellerChange=useCallback(async(newSellerId)=>{
    const found=sellers.find(s=>s.code===newSellerId);
    if(found){
      const newSeries=(found.series || terminalConfig.series || 'VD').toUpperCase();
      const updatedCfg={
        ...terminalConfig,
        sellerId: found.code,
        sellerName: found.name,
        series: newSeries
      };
      setTerminalConfig(updatedCfg);
      await saveConfigTerminal(updatedCfg);
      await recalculateLastOrderNumber(newSeries);
    }
  },[sellers, terminalConfig, recalculateLastOrderNumber]);

  // 7. Sincronización completa (Clientes, Productos, Pedidos pendientes y Max pedido)
  const syncAll=async()=>{
    setSyncing(true);
    setSyncMessage('');
    try{
      // A. Sincronizar pedidos pendientes
      const pending=await getPendingOrders();
      let sentCount=0;
      for(const p of pending){
        try{
          const payload={
            serie: p.serie || p.series,
            numero_pedido: Number(p.numero_pedido || p.number),
            fecha: p.date ? p.date.split('/').reverse().join('-') : new Date().toISOString().split('T')[0],
            id_cliente: Number(p.id_cliente || p.clientCode),
            cliente: p.cliente || p.clientName,
            id_vendedor: Number(p.id_vendedor || terminalConfig.sellerId),
            vendedor: p.vendedor || terminalConfig.sellerName,
            id_forma_pago: Number(p.id_forma_pago || 1),
            id_tarifa: Number(p.id_tarifa || terminalConfig.rateId),
            total: Number(p.total),
            canal: 'PWA',
            lineas: (p.lines||[]).map(l=>({
              code: l.code,
              desc: l.name,
              qty: Number(l.qty),
              price: Number(l.currentPrice != null ? l.currentPrice : (l.price || 0))
            }))
          };
          const res=await createOrderApi(payload);
          await markOrderSynced(p.id_local, res?.id);
          sentCount++;
        }catch(orderErr){
          console.warn('Error reenviando pedido pendiente:', p.id_local, orderErr);
        }
      }

      // B. Refrescar MAX(numero_pedido) para la serie configurada
      const maxServerOrder=await getMaxOrderNumberApi(terminalConfig.series);
      const newMax=Math.max(terminalConfig.lastOrderNumber || 0, maxServerOrder || 0);

      // C. Refrescar maestros (vendedores y tarifas)
      const [remoteSellers, remoteRates]=await Promise.all([
        loadVendedoresApi().catch(()=>[]),
        loadTarifasApi().catch(()=>[])
      ]);
      if(remoteSellers.length) setSellers(remoteSellers);
      if(remoteRates.length) setRates(remoteRates);

      // D. Refrescar productos de la tarifa actual
      const remoteProducts=await loadProductsApi(terminalConfig.rateId);
      if(remoteProducts.length){
        await clearProductCache();
        await cacheProducts(remoteProducts, terminalConfig.rateId);
        setProducts(remoteProducts);
      }

      // E. Refrescar clientes
      const cachedClients=await allCachedClients();
      const initialClients=await loadInitialClientsApi();
      let refreshedClients=[];
      if(cachedClients.length){
        refreshedClients=await syncCachedClientsApi(cachedClients.map(c=>c.code));
      }
      const mergedMap=new Map();
      [...initialClients,...refreshedClients].forEach(c=>{if(c?.code) mergedMap.set(c.code,c);});
      await cacheClients([...mergedMap.values()]);
      setClients(initialClients);

      // Actualizar configuración con nueva fecha y nuevo número
      const nowStr=new Date().toLocaleString('es-ES');
      const updatedCfg={
        ...terminalConfig,
        lastOrderNumber: newMax,
        lastSync: nowStr
      };
      setTerminalConfig(updatedCfg);
      await saveConfigTerminal(updatedCfg);

      // Recargar pedidos: locales y remotos históricos
      const localAll=await getAllOrdersLocal().catch(()=>[]);
      const remoteHistorical=await loadOrdersApi(terminalConfig.sellerId).catch(()=>[]);
      const mergedOrdersMap=new Map();
      remoteHistorical.forEach(ro=>{
        mergedOrdersMap.set(`${ro.series}_${ro.number}`, ro);
      });
      localAll.forEach(lo=>{
        const key=`${lo.series || lo.serie}_${lo.number || lo.numero_pedido}`;
        if(!mergedOrdersMap.has(key) || lo.status==='Pendiente sync' || lo.estado==='pendiente'){
          mergedOrdersMap.set(key, lo);
        }
      });
      setOrders(Array.from(mergedOrdersMap.values()));

      setOnline(true);
      const msg=`Sincronización OK: ${remoteProducts.length} productos, ${initialClients.length} clientes. ${sentCount} pedido(s) pendientes enviados.`;
      setSyncMessage(msg);
      showToast(msg,'ok');
    }catch(err){
      console.error('Error durante la sincronización:',err);
      setOnline(false);
      setSyncMessage('Fallo de conexión al sincronizar. Se mantiene la información local.');
      showToast('Error de conexión al sincronizar','warn');
    }finally{
      setSyncing(false);
    }
  };

  const saveClientLocal=async(saved)=>{
    await cacheClient(saved);
    setClients(current=>{
      const exists=current.some(x=>x.code===saved.code);
      return exists?current.map(x=>x.code===saved.code?saved:x):[saved,...current].slice(0,30);
    });
  };

  const openClientOrders=(c)=>{
    cacheClient(c).catch(console.error);
    setClient(c);
    setTab('clients');
    setScreen('clientOrders');
  };

  // 7. Creación de nuevo pedido: obtiene el siguiente número (MAX + 1)
  const newOrder=async(c)=>{
    const nextNumber=(terminalConfig.lastOrderNumber || 0) + 1;
    const updatedCfg={ ...terminalConfig, lastOrderNumber: nextNumber };
    setTerminalConfig(updatedCfg);
    await saveConfigTerminal(updatedCfg);

    setClient(c);
    setDraft({
      series: terminalConfig.series,
      number: nextNumber,
      sellerName: terminalConfig.sellerName,
      clientCode: c.code,
      lines: []
    });
    setTab('orders');
    setScreen('orderEdit');
  };

  // 8. Finalización de pedido (Fin de pedido)
  const finalize=async(total)=>{
    // FILTRADO ESTRICTO: Solo sobreviven las líneas validadas con qty > 0
    const finalLines=(draft.lines||[]).filter(l=>l.validated===true && Number(l.qty)>0);
    if(finalLines.length===0){
      showToast('No hay líneas validadas en el pedido. Valida con [OK] antes de hacer el pedido.','warn');
      return;
    }

    const calculatedTotal=finalLines.reduce((s,l)=>s+(Number(l.qty)*Number(l.currentPrice!=null?l.currentPrice:(l.price||0))),0);
    downloadPdf({ ...draft, lines: finalLines }, client, calculatedTotal, terminalConfig);

    const localId=`ped_${Date.now()}_${Math.random().toString(36).substring(2,7)}`;
    const pendingRecord={
      id_local: localId,
      id: Date.now(),
      series: draft.series,
      number: draft.number,
      date: new Date().toLocaleDateString('es-ES'),
      clientCode: client.code,
      clientName: client.commercial,
      sellerName: terminalConfig.sellerName,
      total: calculatedTotal,
      status: online ? 'Enviando...' : 'Pendiente sync',
      lines: finalLines.map(x=>({ ...x }))
    };

    // 1. Guardar primero en IndexedDB como pedido pendiente
    await savePendingOrder(pendingRecord);

    const orderPayload={
      serie: String(draft.series || 'VD'),
      numero_pedido: Number(draft.number),
      fecha: new Date().toISOString().split('T')[0],
      id_cliente: Number(client.code) || 0,
      cliente: client.commercial || client.fiscal || `Cliente ${client.code}`,
      id_forma_pago: Number(client.paymentMethodId) || 1,
      id_tarifa: Number(terminalConfig.rateId) || 1,
      total: Number(calculatedTotal.toFixed(2)),
      canal: 'PWA',
      id_vendedor: Number(terminalConfig.sellerId) || 13,
      vendedor: terminalConfig.sellerName || 'Jose',
      lineas: finalLines.map(l=>({
        code: l.code,
        desc: l.name,
        qty: Number(l.qty),
        price: Number(l.currentPrice != null ? l.currentPrice : (l.price || 0))
      }))
    };

    // 2. Si hay conexión, intentar enviar a PostgREST
    let isSynced=false;
    if(online){
      try{
        const serverRes=await createOrderApi(orderPayload);
        await markOrderSynced(localId, serverRes?.id);
        isSynced=true;
      }catch(err){
        console.warn('Error enviando pedido a PostgREST, queda pendiente localmente:', err);
      }
    }

    pendingRecord.status=isSynced ? 'Enviado' : 'Pendiente sync';
    setOrders(prev=>[pendingRecord, ...prev.filter(o=>o.id_local!==localId)]);

    if(isSynced){
      showToast(`✅ Pedido ${draft.series}/${draft.number} registrado en el servidor (${money(calculatedTotal)})`,'ok');
    }else{
      showToast(`📴 Pedido ${draft.series}/${draft.number} guardado en el terminal (${money(calculatedTotal)}) — Se enviará al sincronizar`,'warn');
    }

    setDraft(null);
    setTab('clients');
    setScreen('clientOrders');
  };

  // 9. Añadir producto manual desde catálogo: entra como validated = true
  const addToDraft=(p,qty)=>{
    if(!draft){
      alert('Primero abre un pedido desde un cliente.');
      setModalProduct(null);
      return;
    }
    const currentPrice=p.price != null ? p.price : null;
    const exists=(draft.lines||[]).find(l=>l.code===p.code);

    let updatedLines;
    if(exists){
      updatedLines=draft.lines.map(l=>l.code===p.code?{
        ...l,
        qty: Number(qty),
        validated: true // Confirmado explícitamente al añadir
      }:l);
    }else{
      updatedLines=[
        ...(draft.lines||[]),
        {
          code: p.code,
          name: p.name,
          qty: Number(qty),
          currentPrice: currentPrice,
          price: currentPrice,
          lastPrice: null,
          lastDate: null,
          lastQty: null,
          box: p.box ?? null,
          stock: p.stock ?? null,
          validated: true // Producto añadido manualmente entra validado
        }
      ];
    }

    setDraft({ ...draft, lines: updatedLines });
    setModalProduct(null);
    setTab('orders');
    setScreen('orderEdit');
    showToast(`Añadido: ${p.name} (${qty} uds)`,'ok');
  };

  const openOrder=(o)=>{
    setViewOrder(o);
    setClient(clients.find(c=>String(c.code)===String(o.clientCode))||client);
    setScreen('orderDetail');
  };

  const resendOrder=(o,c)=>{
    downloadPdf(o,c,o.total,terminalConfig);
    window.location.href=`mailto:${encodeURIComponent(c.email)}?subject=${encodeURIComponent(`Pedido ${o.series}/${o.number}`)}&body=${encodeURIComponent(`Adjunta el PDF del pedido ${o.series}/${o.number} que se acaba de generar.`)}`;
  };

  const pendingCount=(orders||[]).filter(o=>o.status==='Pendiente sync').length;

  let content;
  if(tab==='clients'){
    if(screen==='clientOrders'&&client) content=<ClientOrders orders={orders} client={client} onBack={()=>{setClient(null);setScreen('clients')}} onOpen={openOrder} onNew={()=>newOrder(client)} onClientDetail={()=>setScreen('clientDetail')}/>;
    else if(screen==='clientDetail'&&client) content=<ClientDetail client={client} onBack={()=>setScreen('clientOrders')} onEdit={()=>{setEditing(client);setScreen('clientForm')}} onNewOrder={()=>newOrder(client)} onOrders={()=>setScreen('clientOrders')}/>;
    else if(screen==='clientForm') content=<ClientForm value={editing||emptyClient()} isNew={!editing} onCancel={()=>setScreen(editing?'clientDetail':'clients')} onSave={async c=>{const now=new Date().toLocaleString('es-ES');const saved={...c,lastUser:terminalConfig.sellerName,lastUpdate:now,lastSync:c.lastSync||'Nunca'};await saveClientLocal(saved);setClient(saved);setEditing(null);setScreen('clientOrders')}}/>;
    else if(screen==='orderDetail'&&viewOrder) {const c=clients.find(x=>String(x.code)===String(viewOrder.clientCode));content=<OrderDetail order={viewOrder} client={c} onBack={()=>setScreen(client?'clientOrders':'clients')} onPdf={()=>downloadPdf(viewOrder,c,viewOrder.total,terminalConfig)} onResend={()=>resendOrder(viewOrder,c)}/>;}
    else content=<Clients clients={clients} terminalConfig={terminalConfig} onOpen={openClientOrders} onNew={()=>{setEditing(null);setScreen('clientForm')}} onSearch={searchClients} loading={clientSearchLoading} error={clientSearchError}/>;
  } else if(tab==='orders'){
    if(screen==='orderEdit'&&draft&&client) content=<OrderEdit order={draft} setOrder={setDraft} client={client} online={online} onBack={()=>{setTab('clients');setScreen('clientOrders')}} onProducts={()=>{setTab('products');setScreen('products')}} onCatalog={()=>{setTab('products');setScreen('products')}} onFinalize={finalize} products={products} terminalConfig={terminalConfig} showToast={showToast}/>;
    else if(screen==='orderDetail'&&viewOrder) {const c=clients.find(x=>String(x.code)===String(viewOrder.clientCode));content=<OrderDetail order={viewOrder} client={c} onBack={()=>setScreen('orders')} onPdf={()=>downloadPdf(viewOrder,c,viewOrder.total,terminalConfig)} onResend={()=>resendOrder(viewOrder,c)}/>;}
    else content=<OrdersList orders={orders} onOpen={openOrder} onNew={()=>{setTab('clients');setScreen('clients')}}/>;
  } else if(tab==='products' || tab==='catalog') {
    content=<ProductsScreen
      products={products}
      loading={loadingProducts}
      online={online}
      draft={draft}
      rateName={terminalConfig.rateName}
      rateId={terminalConfig.rateId}
      onAdd={p=>setModalProduct(p)}
      onReload={()=>reloadProducts()}
    />;
  } else {
    content=<Config
      terminalConfig={terminalConfig}
      setTerminalConfig={setTerminalConfig}
      online={online}
      setOnline={setOnline}
      sellers={sellers}
      rates={rates}
      pendingCount={pendingCount}
      onSyncAll={syncAll}
      onClearClients={clearClients}
      syncing={syncing}
      syncMessage={syncMessage}
      onTariffChange={handleTariffChange}
      onSeriesChange={handleSeriesChange}
      onSellerChange={handleSellerChange}
      onRecalculateOrderNumber={recalculateLastOrderNumber}
    />;
  }

  const changeTab=(t)=>{
    setTab(t);
    if(t==='clients') setScreen(client?'clientOrders':'clients');
    else if(t==='orders') setScreen(draft?'orderEdit':'orders');
    else if(t==='products' || t==='catalog') setScreen('products');
    else setScreen('config');
  };

  return <Layout tab={tab} setTab={changeTab} online={online}>
    {content}
    {modalProduct&&<AddModal product={modalProduct} onClose={()=>setModalProduct(null)} onAdd={qty=>addToDraft(modalProduct,qty)}/>}
    <Toast msg={toast.msg} type={toast.type} onClose={hideToast}/>
  </Layout>;
}

const money=n=>new Intl.NumberFormat('es-ES',{style:'currency',currency:'EUR'}).format(n||0);
createRoot(document.getElementById('root')).render(<App/>);
