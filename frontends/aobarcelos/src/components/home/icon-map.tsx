import {
  Award,
  BookOpen,
  Calendar,
  Feather,
  GraduationCap,
  HeartHandshake,
  Landmark,
  MapPin,
  Newspaper,
  ShieldCheck,
  Sparkles,
  Trophy,
  Users,
  type LucideIcon,
} from "lucide-react";

const MAP: Record<string, LucideIcon> = {
  Award,
  Calendar,
  BookOpen,
  Users,
  Feather,
  Trophy,
  HeartHandshake,
  Sparkles,
  MapPin,
  GraduationCap,
  Landmark,
  Newspaper,
  ShieldCheck,
};

export function AreaIcon({ name, className }: { name: string; className?: string }) {
  const Icon = MAP[name] ?? Award;
  return <Icon className={className} strokeWidth={1.5} aria-hidden />;
}
