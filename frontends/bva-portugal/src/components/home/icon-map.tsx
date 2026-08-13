import {
  Award,
  BookOpen,
  Calendar,
  Dna,
  Feather,
  GraduationCap,
  HeartHandshake,
  Landmark,
  MapPin,
  Newspaper,
  Plane,
  Sparkles,
  Trophy,
  Users,
  type LucideIcon,
} from "lucide-react";

const MAP: Record<string, LucideIcon> = {
  Award,
  Calendar,
  BookOpen,
  Dna,
  Users,
  Feather,
  Plane,
  Trophy,
  HeartHandshake,
  Sparkles,
  MapPin,
  GraduationCap,
  Landmark,
  Newspaper,
};

export function AreaIcon({ name, className }: { name: string; className?: string }) {
  const Icon = MAP[name] ?? Award;
  return <Icon className={className} strokeWidth={1.5} aria-hidden />;
}
