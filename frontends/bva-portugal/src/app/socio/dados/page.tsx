import { apiGet } from "@/lib/socio-auth";
import { DadosForm } from "./dados-form";

type Profile = {
  id: number;
  numeroSocio: string;
  nomeCompleto: string;
  email: string;
  telefone: string | null;
  nif: string | null;
  dataNascimento: string | null;
  morada: string | null;
  codigoPostal: string | null;
  localidade: string | null;
  fotoPath: string | null;
  especiesInteresse: string[];
};

export const metadata = { title: "Os meus dados" };

export default async function DadosPage() {
  const me = await apiGet<Profile>("/api/me");
  if (!me) return <p>Sem perfil.</p>;
  return (
    <div className="max-w-2xl">
      <h1 className="text-3xl font-bold">Os meus dados</h1>
      <p className="mt-2 text-sm text-ink-500">Nº de sócio {me.numeroSocio} · Email: {me.email}</p>
      <div className="mt-6 rounded-lg border border-sand-300 bg-white p-6 shadow-sm">
        <DadosForm initial={me} />
      </div>
    </div>
  );
}
