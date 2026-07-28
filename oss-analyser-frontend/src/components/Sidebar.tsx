import { NavLink } from "react-router-dom";

const Sidebar = () => {
    return (
        <aside className="sidebar">
            <div className="sidebarlogo">
                <h2>OSS Analyzer</h2>
            </div>
            <nav className="sidebar-nav">
                <NavLink to="/" className="nav-link">
                    Dashboard
                </NavLink>
                <NavLink to="/analyze" className="nav-link">
                    Analyze
                </NavLink>
                <NavLink to="/repositories" className="nav-link">
                    Repositories
                </NavLink>
            </nav>
        </aside>
    );
};

export default Sidebar;