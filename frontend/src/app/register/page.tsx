"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { AuthShell } from "@/components/auth-shell";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";
import { Spinner } from "@/components/ui/misc";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";

const schema = z.object({
  name: z.string().min(1, "Name is required").max(200),
  email: z.string().min(1, "Email is required").email("Enter a valid email"),
  password: z.string().min(8, "Use at least 8 characters").max(128),
});

type FormValues = z.infer<typeof schema>;

export default function RegisterPage() {
  const { register: registerUser, status } = useAuth();
  const router = useRouter();
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  useEffect(() => {
    if (status === "authenticated") router.replace("/dashboard");
  }, [status, router]);

  async function onSubmit(values: FormValues) {
    setFormError(null);
    try {
      await registerUser(values.email, values.name, values.password);
      router.push("/dashboard");
    } catch (error) {
      setFormError(
        error instanceof ApiError ? error.message : "Unable to create account. Please try again.",
      );
    }
  }

  return (
    <AuthShell
      eyebrow="Get started"
      title={
        <>
          Create <span className="italic text-accent">account</span>
        </>
      }
      subtitle="Spin up your first project in under a minute."
      footer={
        <>
          Already have an account?{" "}
          <Link href="/login" className="text-accent hover:underline">
            Log in
          </Link>
        </>
      }
    >
      <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4" noValidate>
        <Field label="Name" htmlFor="name" error={errors.name?.message}>
          <Input id="name" autoComplete="name" placeholder="Ada Lovelace" {...register("name")} />
        </Field>
        <Field label="Email" htmlFor="email" error={errors.email?.message}>
          <Input id="email" type="email" autoComplete="email" placeholder="you@example.com" {...register("email")} />
        </Field>
        <Field
          label="Password"
          htmlFor="password"
          error={errors.password?.message}
          hint="At least 8 characters."
        >
          <Input id="password" type="password" autoComplete="new-password" placeholder="••••••••" {...register("password")} />
        </Field>

        {formError && (
          <p className="rounded-md border border-danger/30 bg-danger/10 px-3 py-2 text-sm text-danger">
            {formError}
          </p>
        )}

        <Button type="submit" disabled={isSubmitting} className="mt-1 w-full">
          {isSubmitting ? <Spinner /> : "Create account"}
        </Button>
      </form>
    </AuthShell>
  );
}
