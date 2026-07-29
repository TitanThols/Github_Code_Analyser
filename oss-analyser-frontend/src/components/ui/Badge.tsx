import type { Severity } from "../../types";

interface BadgeProps {
    severity: Severity
}

const Badge = ({ severity }: BadgeProps) => {
    const getColorClass = (severity: Severity) => {
        switch (severity) {
            case "Critical":
                return 'badge-critical';
            case "High":
                return 'badge-high';
            case "Medium":
                return "badge-medium";
            case "Low":
                return 'badge-low';
        }
    };

    return (
        <span className={`badge ${getColorClass(severity)}`}>
            {severity}
        </span>
    );
};

export default Badge;