CREATE TABLE IF NOT EXISTS public.pedidos_nuevos (
    id BIGSERIAL PRIMARY KEY,
    id_empresa INTEGER DEFAULT 1,
    ejercicio INTEGER DEFAULT EXTRACT(YEAR FROM CURRENT_DATE),
    serie VARCHAR(10) DEFAULT '1',
    numero_pedido INTEGER,
    fecha TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    id_cliente BIGINT NOT NULL,
    cliente VARCHAR(255),
    id_forma_pago INTEGER DEFAULT 1,
    id_tarifa INTEGER DEFAULT 1,
    total NUMERIC(12,2) NOT NULL,
    canal VARCHAR(50) DEFAULT 'movil',
    id_vendedor INTEGER DEFAULT 1,
    vendedor VARCHAR(100),
    lineas JSONB NOT NULL DEFAULT '[]'::jsonb,
    estado VARCHAR(20) DEFAULT 'N',
    synced_at TIMESTAMP WITH TIME ZONE DEFAULT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE OR REPLACE VIEW api.pedidos AS
SELECT 
    id,
    id_empresa,
    ejercicio,
    serie,
    numero_pedido,
    fecha,
    id_cliente,
    cliente,
    id_forma_pago,
    id_tarifa,
    total,
    canal,
    id_vendedor,
    vendedor,
    lineas,
    estado,
    synced_at,
    created_at
FROM public.pedidos_nuevos;

CREATE OR REPLACE VIEW api.pedidos_nuevos AS
SELECT * FROM api.pedidos;

GRANT ALL ON TABLE public.pedidos_nuevos TO postgres, authenticator, web_anon;
GRANT ALL ON SEQUENCE public.pedidos_nuevos_id_seq TO postgres, authenticator, web_anon;
GRANT ALL ON api.pedidos TO postgres, authenticator, web_anon;
GRANT ALL ON api.pedidos_nuevos TO postgres, authenticator, web_anon;

NOTIFY pgrst, 'reload schema';
