defmodule ElixirKit do
  def start do
    {:ok, _} = ElixirKit.Server.start_link(self())
  end
end
