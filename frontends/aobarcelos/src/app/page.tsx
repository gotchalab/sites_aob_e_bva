import type { Metadata } from "next";
import { api, parseAnnouncement, parseHomeConfig } from "@/lib/api";
import { AnnouncementBar } from "@/components/home/announcement-bar";
import { HeroSection } from "@/components/home/hero-section";
import { StatsBar } from "@/components/home/stats-bar";
import { MissionBlock } from "@/components/home/mission-block";
import { AreasGrid } from "@/components/home/areas-grid";
import { FeaturedArticles } from "@/components/home/featured-articles";
import { NewsGrid } from "@/components/home/news-grid";
import { JoinCta } from "@/components/home/join-cta";
import { SponsorsGrid } from "@/components/home/sponsors-grid";

export const revalidate = 60;

const NEWS_LIMIT = 8;
const FEATURED_LIMIT = 4;

export async function generateMetadata(): Promise<Metadata> {
  const site = await api.site().catch(() => null);
  return {
    title: { absolute: site?.name ?? "AOB" },
    description: site?.tagline ?? site?.description ?? undefined,
  };
}

export default async function HomePage() {
  const [site, featuredList, recentList, sponsors] = await Promise.all([
    api.site(),
    api.articles({ featured: true, limit: FEATURED_LIMIT }).catch(() => ({ items: [], total: 0, page: 1, pageSize: FEATURED_LIMIT })),
    api.articles({ limit: NEWS_LIMIT }),
    api.sponsors().catch(() => []),
  ]);

  const home = parseHomeConfig(site);
  const announcement = parseAnnouncement(site);

  // Se há featured, o hero mostra o primeiro featured; se não, mostra o mais recente.
  const heroFeatured = featuredList.items[0] ?? recentList.items[0] ?? null;
  const featuredRest = featuredList.items;
  const recent = recentList.items.slice(0, NEWS_LIMIT);

  return (
    <>
      <AnnouncementBar announcement={announcement} />
      <HeroSection site={site} home={home} featured={heroFeatured} />
      <NewsGrid items={recent} total={recentList.total} />
      <StatsBar home={home} />
      <MissionBlock home={home} />
      <AreasGrid home={home} />
      {featuredRest.length > 0 && (
        <FeaturedArticles items={featuredRest} skipSlug={heroFeatured?.slug} />
      )}
      <JoinCta home={home} />
      <SponsorsGrid sponsors={sponsors} />
    </>
  );
}
