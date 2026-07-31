import { useEffect } from 'react';

interface ToastProps {
  message: string;
  type?: 'info' | 'success' | 'warning';
  onClose: () => void;
}

const Toast = ({ message, type = 'info', onClose }: ToastProps) => {
  useEffect(() => {
    const timer = window.setTimeout(onClose, 3200);
    return () => window.clearTimeout(timer);
  }, [onClose]);

  return (
    <div className={`toast toast-${type}`}>
      <div>{message}</div>
      <button type="button" onClick={onClose} className="toast-close">×</button>
    </div>
  );
};

export default Toast;
