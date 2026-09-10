import { useState } from 'react';
import { verifyPin, clearVendedorActivo } from '../db';
import { CONFIG } from '../config';

export default function PinScreen({ onUnlock }) {
  const [pin, setPin] = useState('');
  const [error, setError] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const PIN_LENGTH = CONFIG.PIN_LENGTH;

  const handleNumber = (num) => {
    if (pin.length < PIN_LENGTH && !isLoading) {
      const newPin = pin + num;
      setPin(newPin);
      setError(false);
      if (newPin.length === PIN_LENGTH) {
        setTimeout(() => trySubmit(newPin), 100);
      }
    }
  };

  const handleBackspace = () => {
    if (!isLoading) {
      setPin(prev => prev.slice(0, -1));
      setError(false);
    }
  };

  const trySubmit = async (pinToCheck) => {
    if (pinToCheck.length !== PIN_LENGTH || isLoading) return;
    setIsLoading(true);
    const isValid = await verifyPin(pinToCheck);
    if (isValid) {
      onUnlock();
    } else {
      setError(true);
      setPin('');
      setIsLoading(false);
    }
  };

  const keys = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '', '0', '⌫'];

  return (
    <div className="pin-screen-overlay">
      <div className="pin-container">
        <h2 className="pin-title">REPREDISL</h2>
        <p className="pin-subtitle">Introduce tu PIN de acceso</p>

        <div className={`pin-dots ${error ? 'shake' : ''}`}>
          {[...Array(PIN_LENGTH)].map((_, i) => (
            <div key={i} className={`pin-dot ${i < pin.length ? 'filled' : ''}`} />
          ))}
        </div>

        {error && <p className="pin-error">PIN incorrecto. Inténtalo de nuevo.</p>}

        <div className="pin-keypad">
          {keys.map((key, idx) => {
            if (key === '') return <div key={idx} className="pin-key empty" />;
            const isBackspace = key === '⌫';
            return (
              <button
                key={idx}
                type="button"
                className={`pin-key ${isBackspace ? 'backspace' : ''}`}
                onClick={() => isBackspace ? handleBackspace() : handleNumber(key)}
                disabled={isLoading}
              >
                {key}
              </button>
            );
          })}
        </div>

        <button
          className="pin-logout"
          onClick={() => { clearVendedorActivo(); window.location.reload(); }}
          disabled={isLoading}
        >
          Cambiar de vendedor / Dispositivo
        </button>
      </div>
    </div>
  );
}