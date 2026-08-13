import { LoginForm } from "./login-form";

export const metadata = { title: "Área de sócio · Login" };

export default async function SocioLoginPage({
  searchParams,
}: {
  searchParams: Promise<{ redirect?: string; error?: string }>;
}) {
  const sp = await searchParams;
  return (
    <div className="mx-auto max-w-md">
      <h1 className="text-3xl font-bold">Área de sócio</h1>
      <p className="mt-2 text-sm text-ink-600">Entra com o email e a password recebidos.</p>
      <div className="mt-6 rounded-lg border border-sand-300 bg-white p-6 shadow-sm">
        <LoginForm redirect={sp.redirect} initialError={sp.error} />
      </div>
      <div className="mt-6 rounded-lg border border-sand-300 bg-sand-100 p-4 text-sm text-ink-700">
        Ainda não é sócio?{" "}
        <a href="/inscricao-socio" className="font-medium text-brand-700 underline underline-offset-2 hover:text-brand-800">
          Inscreva-se aqui
        </a>
      </div>
    </div>
  );
}
