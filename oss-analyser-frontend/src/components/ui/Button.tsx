interface ButtonProps {
    label: string;
    onClick: () => void;
    variant?: 'primary' | 'secondary';
    disabled?: boolean;
}

const Button = ({ label, onClick, variant = 'primary', disabled = false }: ButtonProps) => {
    return (
        <button
            className={`btn btn-${variant}`}
            onClick={onClick}
            disabled={disabled}
        >
            {label}
        </button>
    );
}

export default Button;

