import type { ReactNode } from "react";

interface CardProps {
    title?: string,
    children: ReactNode;
}

const Card = ({ title, children }: CardProps) => {
    return (
        <div className="card">
            {title && <h3 className="card-title">{title}</h3>}
            {children}
        </div>
    );
};

export default Card;
