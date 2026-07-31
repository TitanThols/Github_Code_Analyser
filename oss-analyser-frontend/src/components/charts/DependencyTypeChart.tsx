import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';

interface DependencyTypeDatum {
  name: string;
  value: number;
}

interface DependencyTypeChartProps {
  data: DependencyTypeDatum[];
}

const DependencyTypeChart = ({ data }: DependencyTypeChartProps) => {
  return (
    <div className="chart-shell">
      <ResponsiveContainer width="100%" height={260}>
        <BarChart data={data}>
          <CartesianGrid stroke="rgba(255,255,255,0.08)" vertical={false} />
          <XAxis dataKey="name" tickLine={false} axisLine={false} />
          <YAxis tickLine={false} axisLine={false} allowDecimals={false} />
          <Tooltip />
          <Bar dataKey="value" radius={[8, 8, 0, 0]} fill="#34d399" />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
};

export default DependencyTypeChart;
